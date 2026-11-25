import { useEffect, useMemo, useRef, useState } from 'react';
import { CheckCircle2, CircleAlert, Download, RefreshCw, Trash2, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { jsonBody, localApi } from './localApi.js';

// ---------------------------------------------------------------------------------------------
// Model pulls run in the client worker. This store polls their state for every view that cares
// (the topbar indicator and any open Manage models dialog) so a pull keeps showing progress no
// matter which page or dialog started it.
// ---------------------------------------------------------------------------------------------

const pullStore = {
  jobs: [],
  listeners: new Set(),
  timer: null,
  inFlight: false
};

function emitPulls(jobs) {
  pullStore.jobs = jobs;
  pullStore.listeners.forEach((listener) => listener(jobs));
}

async function pollPulls() {
  if (pullStore.inFlight) return;
  pullStore.inFlight = true;
  try {
    const response = await localApi('/api/v1/model-pulls');
    const previous = pullStore.jobs;
    const jobs = Array.isArray(response.jobs) ? response.jobs : [];
    emitPulls(jobs);
    const finished = jobs.filter((job) => !isActive(job) && previous.some((old) => old.id === job.id && isActive(old)));
    if (finished.length > 0) window.dispatchEvent(new CustomEvent('xeno:model-pulls-finished', { detail: finished }));
  } catch {
    // The indicator stays quiet when the worker is unreachable; pages show their own errors.
  } finally {
    pullStore.inFlight = false;
    schedulePulls();
  }
}

function schedulePulls() {
  window.clearTimeout(pullStore.timer);
  if (pullStore.listeners.size === 0) return;
  const delay = pullStore.jobs.some(isActive) ? 1000 : 15000;
  pullStore.timer = window.setTimeout(pollPulls, delay);
}

/** Ask the store to poll now, for example right after starting a pull. */
export function refreshModelPulls() {
  window.clearTimeout(pullStore.timer);
  pollPulls();
}

/** Every model pull the client worker knows, newest first, kept fresh while the component is mounted. */
export function useModelPulls() {
  const [jobs, setJobs] = useState(pullStore.jobs);
  useEffect(() => {
    pullStore.listeners.add(setJobs);
    if (pullStore.listeners.size === 1) pollPulls();
    return () => {
      pullStore.listeners.delete(setJobs);
      if (pullStore.listeners.size === 0) window.clearTimeout(pullStore.timer);
    };
  }, []);
  return jobs;
}

/** Run a callback whenever a pull finishes (any outcome). */
export function useModelPullsFinished(callback) {
  const ref = useRef(callback);
  useEffect(() => { ref.current = callback; }, [callback]);
  useEffect(() => {
    function handle(event) { ref.current?.(event.detail || []); }
    window.addEventListener('xeno:model-pulls-finished', handle);
    return () => window.removeEventListener('xeno:model-pulls-finished', handle);
  }, []);
}

function isActive(job) {
  return job?.state === 'Queued' || job?.state === 'Running';
}

export function formatSize(bytes) {
  const number = Number(bytes);
  if (!Number.isFinite(number) || number <= 0) return '-';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  let value = number;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit += 1;
  }
  return `${value >= 100 || unit === 0 ? value.toFixed(0) : value.toFixed(1)} ${units[unit]}`;
}

function formatModified(value) {
  const time = Date.parse(value || '');
  if (Number.isNaN(time)) return value || '-';
  return new Date(time).toLocaleDateString([], { year: 'numeric', month: 'short', day: 'numeric' });
}

function providerPath(name) {
  return `/api/v1/providers/${encodeURIComponent(name)}`;
}

function isOllama(provider) {
  return String(provider?.apiType || '').toLowerCase() === 'ollama';
}

// ---------------------------------------------------------------------------------------------
// Pull progress
// ---------------------------------------------------------------------------------------------

export function PullProgressRow({ job, onCancel }) {
  const { t } = useTranslation();
  const active = isActive(job);
  const percent = job.percent == null ? null : Math.max(0, Math.min(100, Number(job.percent)));
  const tone = job.state === 'Failed' ? 'danger' : job.state === 'Cancelled' ? 'neutral' : job.state === 'Succeeded' ? 'success' : 'info';
  return (
    <div className={`model-pull-row ${tone}`} role="status" aria-live="polite">
      <div className="model-pull-row-heading">
        <div>
          <strong>{job.model}</strong>
          <span>{job.provider}</span>
        </div>
        <span className={`status-chip ${tone}`}>{t(`pullState${job.state}`, job.state)}</span>
        {active && onCancel ? (
          <button className="icon-button" onClick={() => onCancel(job)} title={t('tooltipCancelPull')} type="button"><X size={15} /></button>
        ) : null}
      </div>
      <div className={`model-pull-meter${percent == null && active ? ' indeterminate' : ''}`} aria-label={t('modelPullProgress')} aria-valuemax={100} aria-valuemin={0} aria-valuenow={percent ?? undefined} role="progressbar">
        <span style={{ width: `${job.state === 'Succeeded' ? 100 : percent ?? (active ? 30 : 0)}%` }} />
      </div>
      <div className="model-pull-row-meta">
        <span>{job.error || job.status || '-'}</span>
        <span>{job.totalBytes > 0 ? `${formatSize(job.completedBytes)} / ${formatSize(job.totalBytes)}` : ''}{percent != null ? ` · ${percent.toFixed(1)}%` : ''}</span>
      </div>
      {job.state === 'Succeeded' && job.advertised ? <small className="model-pull-note"><CheckCircle2 size={13} />{t('pullAdvertised')}</small> : null}
    </div>
  );
}

async function cancelPull(job) {
  await localApi(`/api/v1/model-pulls/${encodeURIComponent(job.id)}`, { method: 'DELETE' });
  refreshModelPulls();
}

/** Topbar indicator: shows active pulls and opens the downloads list. Hidden when there is nothing to show. */
export function ModelPullsIndicator({ onOpen }) {
  const { t } = useTranslation();
  const jobs = useModelPulls();
  const active = jobs.filter(isActive);
  const failedRecently = jobs.filter((job) => job.state === 'Failed' && job.finishedUtc && Date.now() - Date.parse(job.finishedUtc) < 10 * 60 * 1000);
  if (active.length === 0 && failedRecently.length === 0) return null;
  const known = active.filter((job) => job.totalBytes > 0);
  const total = known.reduce((sum, job) => sum + job.totalBytes, 0);
  const done = known.reduce((sum, job) => sum + job.completedBytes, 0);
  const percent = total > 0 ? Math.round((done * 100) / total) : null;
  const label = active.length === 0
    ? t('pullsFailedShort', { count: failedRecently.length })
    : active.length === 1
      ? t('pullingOne', { model: active[0].model, percent: percent == null ? '…' : `${percent}%` })
      : t('pullingMany', { count: active.length, percent: percent == null ? '…' : `${percent}%` });
  return (
    <button className={`topbar-pulls${active.length === 0 ? ' failed' : ''}`} onClick={onOpen} title={t('tooltipModelPulls')} type="button">
      {active.length === 0 ? <CircleAlert size={15} /> : <Download size={15} />}
      <span>{label}</span>
      {percent != null ? <span className="topbar-pulls-meter"><span style={{ width: `${percent}%` }} /></span> : null}
    </button>
  );
}

/** Body of the downloads modal: every pull this worker knows. */
export function ModelPullsPanel() {
  const { t } = useTranslation();
  const jobs = useModelPulls();
  async function clearFinished() {
    await localApi('/api/v1/model-pulls/clear', { method: 'POST', body: '{}' });
    refreshModelPulls();
  }
  return (
    <div className="model-pulls-panel">
      <div className="modal-toolbar">
        <p className="muted-text">{t('modelPullsIntro')}</p>
        <button className="action-button" disabled={!jobs.some((job) => !isActive(job))} onClick={clearFinished} title={t('tooltipClearFinishedPulls')} type="button">{t('clearFinished')}</button>
      </div>
      {jobs.length === 0 ? <div className="empty-state">{t('noModelPulls')}</div> : jobs.map((job) => <PullProgressRow job={job} key={job.id} onCancel={cancelPull} />)}
    </div>
  );
}

// ---------------------------------------------------------------------------------------------
// Manage models on one provider
// ---------------------------------------------------------------------------------------------

export function ProviderModelsDialog({ onChanged, provider }) {
  const { t } = useTranslation();
  const [inventory, setInventory] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [pullName, setPullName] = useState('');
  const [advertise, setAdvertise] = useState(false);
  const [confirming, setConfirming] = useState('');
  const [busy, setBusy] = useState('');
  const [query, setQuery] = useState('');
  const jobs = useModelPulls();
  const providerJobs = jobs.filter((job) => job.provider === provider.name && (isActive(job) || (job.finishedUtc && Date.now() - Date.parse(job.finishedUtc) < 5 * 60 * 1000)));
  const manageable = inventory ? inventory.canPull : isOllama(provider);

  async function load() {
    setLoading(true);
    setError('');
    try {
      setInventory(await localApi(`${providerPath(provider.name)}/models`));
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { load(); }, [provider.name]);
  useModelPullsFinished((finished) => {
    if (finished.some((job) => job.provider === provider.name)) {
      load();
      onChanged?.();
    }
  });

  async function pull(event) {
    event.preventDefault();
    const model = pullName.trim();
    if (!model) return;
    setError('');
    setNotice('');
    try {
      await localApi(`${providerPath(provider.name)}/pulls`, { method: 'POST', body: jsonBody({ model, advertise }) });
      setPullName('');
      setNotice(t('pullStarted', { model }));
      refreshModelPulls();
    } catch (err) {
      setError(err.message);
    }
  }

  async function remove(model) {
    setBusy(model.name);
    setError('');
    setNotice('');
    try {
      const result = await localApi(`${providerPath(provider.name)}/models/${encodeURIComponent(model.name)}`, { method: 'DELETE' });
      setNotice(result.advertisementsRemoved > 0 ? t('modelDeletedAndUnadvertised', { model: model.name }) : t('modelDeleted', { model: model.name }));
      setConfirming('');
      await load();
      onChanged?.();
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy('');
    }
  }

  const models = Array.isArray(inventory?.models) ? inventory.models : [];
  const normalized = query.trim().toLowerCase();
  const visible = normalized ? models.filter((model) => model.name.toLowerCase().includes(normalized)) : models;
  const totalSize = models.reduce((sum, model) => sum + (Number(model.sizeBytes) || 0), 0);

  return (
    <div className="provider-models-dialog">
      <div className="provider-models-summary">
        <span className="status-chip neutral">{provider.apiType}</span>
        <code>{provider.endpoint}</code>
        {inventory?.reachable ? <span className="muted-text">{t('providerModelsSummary', { count: models.length, size: formatSize(totalSize) })}</span> : null}
        <button className="icon-button" disabled={loading} onClick={load} title={t('tooltipRefreshProviderModels')} type="button"><RefreshCw size={15} /></button>
      </div>
      {error ? <div className="settings-restart-banner error-state" role="alert"><CircleAlert size={16} /><span>{error}</span></div> : null}
      {notice ? <div className="settings-restart-banner" role="status"><CheckCircle2 size={16} /><span>{notice}</span></div> : null}
      {inventory && !inventory.reachable ? <div className="settings-restart-banner error-state" role="alert"><CircleAlert size={16} /><span>{t('providerUnreachable', { error: inventory.error || t('unknown') })}</span></div> : null}
      {manageable ? (
        <form className="provider-pull-form" onSubmit={pull}>
          <label className="form-field" title={t('tooltipPullModelName')}>
            <span>{t('pullModel')}</span>
            <input autoComplete="off" onChange={(event) => setPullName(event.target.value)} placeholder="llama3.2:3b" title={t('tooltipPullModelName')} value={pullName} />
          </label>
          <label className="toggle-field" title={t('tooltipPullAdvertise')}>
            <input checked={advertise} onChange={(event) => setAdvertise(event.target.checked)} type="checkbox" />
            {t('advertiseWhenFinished')}
          </label>
          <button className="primary-button" disabled={!pullName.trim()} title={t('tooltipStartPull')} type="submit"><Download size={15} />{t('pull')}</button>
        </form>
      ) : <div className="empty-state">{t('modelManagementOllamaOnly', { apiType: provider.apiType })}</div>}
      {providerJobs.length > 0 ? (
        <div className="provider-pull-list">
          {providerJobs.map((job) => <PullProgressRow job={job} key={job.id} onCancel={cancelPull} />)}
          <small className="muted-text">{t('pullsContinueInBackground')}</small>
        </div>
      ) : null}
      <div className="provider-models-toolbar">
        <label className="search-box" title={t('tooltipSearchTable')}>
          <input aria-label={t('search')} onChange={(event) => setQuery(event.target.value)} placeholder={t('search')} value={query} />
        </label>
      </div>
      <div className="table-scroll">
        <table className="data-table provider-models-table">
          <thead>
            <tr>
              <th>{t('model')}</th>
              <th>{t('size')}</th>
              <th>{t('parameters')}</th>
              <th>{t('quantization')}</th>
              <th>{t('modified')}</th>
              <th>{t('advertised')}</th>
              {manageable ? <th className="actions-cell">{t('actions')}</th> : null}
            </tr>
          </thead>
          <tbody>
            {loading && !inventory ? (
              <tr><td className="empty-cell" colSpan={7}>{t('loading')}</td></tr>
            ) : visible.length === 0 ? (
              <tr><td className="empty-cell" colSpan={7}>{inventory?.reachable ? t('noProviderModels') : '-'}</td></tr>
            ) : visible.map((model) => (
              <tr key={model.name}>
                <td><code>{model.name}</code>{model.remote ? <span className="status-chip info inline-chip">{t('remoteModel')}</span> : null}</td>
                <td>{formatSize(model.sizeBytes)}</td>
                <td>{model.parameterSize || '-'}</td>
                <td>{model.quantization || '-'}</td>
                <td>{formatModified(model.modified)}</td>
                <td>{model.advertised ? <span className="status-chip success">{t('yes')}</span> : <span className="muted-text">{t('no')}</span>}</td>
                {manageable ? (
                  <td className="actions-cell">
                    {confirming === model.name ? (
                      <span className="inline-confirm">
                        <button className="action-button danger" disabled={busy === model.name} onClick={() => remove(model)} title={t('tooltipConfirmDeleteProviderModel')} type="button">{busy === model.name ? t('deleting') : t('confirmDelete')}</button>
                        <button className="action-button" onClick={() => setConfirming('')} title={t('cancel')} type="button">{t('cancel')}</button>
                      </span>
                    ) : (
                      <button className="icon-button danger" onClick={() => setConfirming(model.name)} title={model.advertised ? t('tooltipDeleteProviderModelAdvertised') : t('tooltipDeleteProviderModel')} type="button"><Trash2 size={15} /></button>
                    )}
                  </td>
                ) : null}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

// ---------------------------------------------------------------------------------------------
// Import advertised models from providers
// ---------------------------------------------------------------------------------------------

export function ImportModelsDialog({ formId, onImported, providers }) {
  const { t } = useTranslation();
  const [selectedProviders, setSelectedProviders] = useState(() => providers.map((item) => item.name));
  const [inventories, setInventories] = useState({});
  const [picked, setPicked] = useState({});
  const [kinds, setKinds] = useState({});
  const [error, setError] = useState('');
  const [result, setResult] = useState(null);

  async function loadProvider(name) {
    setInventories((current) => ({ ...current, [name]: { loading: true } }));
    try {
      const inventory = await localApi(`${providerPath(name)}/models`);
      setInventories((current) => ({ ...current, [name]: inventory }));
      setPicked((current) => {
        const next = { ...current };
        (inventory.models || []).forEach((model) => {
          const key = `${name}\u0000${model.name}`;
          if (!(key in next)) next[key] = !model.advertised;
        });
        return next;
      });
      setKinds((current) => {
        const next = { ...current };
        (inventory.models || []).forEach((model) => {
          const key = `${name}\u0000${model.name}`;
          if (!(key in next)) next[key] = model.looksLikeEmbeddings ? 'embeddings' : 'completions';
        });
        return next;
      });
    } catch (err) {
      setInventories((current) => ({ ...current, [name]: { provider: name, reachable: false, error: err.message, models: [] } }));
    }
  }

  useEffect(() => {
    selectedProviders.forEach((name) => {
      if (!inventories[name]) loadProvider(name);
    });
  }, [selectedProviders.join('|')]);

  function toggleProvider(name) {
    setSelectedProviders((current) => current.includes(name) ? current.filter((item) => item !== name) : [...current, name]);
  }

  const candidates = useMemo(() => selectedProviders.flatMap((name) => {
    const inventory = inventories[name];
    return (inventory?.models || []).map((model) => ({ ...model, provider: name, key: `${name}\u0000${model.name}` }));
  }), [inventories, selectedProviders.join('|')]);
  const chosen = candidates.filter((model) => !model.advertised && picked[model.key]);
  const available = candidates.filter((model) => !model.advertised);

  function setAll(value) {
    setPicked((current) => {
      const next = { ...current };
      available.forEach((model) => { next[model.key] = value; });
      return next;
    });
  }

  async function submit(event) {
    event.preventDefault();
    setError('');
    if (chosen.length === 0) {
      setError(t('importNothingSelected'));
      return;
    }
    try {
      const response = await localApi('/api/v1/models/import', {
        method: 'POST',
        body: jsonBody({
          models: chosen.map((model) => {
            const kind = kinds[model.key] || 'completions';
            return { provider: model.provider, name: model.name, supportsCompletions: kind !== 'embeddings', supportsEmbeddings: kind !== 'completions' };
          })
        })
      });
      setResult(response);
      onImported?.(response);
      selectedProviders.forEach((name) => loadProvider(name));
      setPicked({});
    } catch (err) {
      setError(err.message);
    }
  }

  return (
    <form className="import-models-dialog" id={formId} onSubmit={submit}>
      <p className="muted-text">{t('importModelsIntro')}</p>
      <fieldset className="import-provider-picker">
        <legend>{t('importFromProviders')}</legend>
        {providers.length === 0 ? <div className="empty-state">{t('noProvidersToImport')}</div> : providers.map((provider) => {
          const inventory = inventories[provider.name];
          const count = (inventory?.models || []).filter((model) => !model.advertised).length;
          return (
            <label className="import-provider-option" key={provider.name} title={t('tooltipImportProvider', { name: provider.name })}>
              <input checked={selectedProviders.includes(provider.name)} onChange={() => toggleProvider(provider.name)} type="checkbox" />
              <strong>{provider.name}</strong>
              <span className="muted-text">{provider.apiType}</span>
              {selectedProviders.includes(provider.name) ? (
                inventory?.loading ? <span className="muted-text">{t('loading')}</span>
                  : inventory && inventory.reachable === false ? <span className="status-chip danger" title={inventory.error}>{t('unreachable')}</span>
                    : inventory ? <span className="status-chip neutral">{t('newModelsCount', { count })}</span> : null
              ) : null}
            </label>
          );
        })}
      </fieldset>
      {error ? <div className="settings-restart-banner error-state" role="alert"><CircleAlert size={16} /><span>{error}</span></div> : null}
      {result ? (
        <div className="settings-restart-banner" role="status">
          <CheckCircle2 size={16} />
          <span>{t('importResult', { added: result.added?.length || 0, skipped: result.skipped?.length || 0, failed: result.failed?.length || 0 })}{result.failed?.length ? ` ${result.failed.join('; ')}` : ''}</span>
        </div>
      ) : null}
      {selectedProviders.map((name) => inventories[name]).filter((inventory) => inventory && inventory.reachable === false).map((inventory) => (
        <div className="settings-restart-banner error-state" key={inventory.provider || inventory.error}><CircleAlert size={16} /><span>{t('providerUnreachableNamed', { name: inventory.provider || '', error: inventory.error || t('unknown') })}</span></div>
      ))}
      <div className="import-models-toolbar">
        <span className="muted-text">{t('importSelectedCount', { count: chosen.length, total: available.length })}</span>
        <span className="button-row">
          <button className="action-button" disabled={available.length === 0} onClick={() => setAll(true)} title={t('tooltipSelectAllModels')} type="button">{t('selectAll')}</button>
          <button className="action-button" disabled={available.length === 0} onClick={() => setAll(false)} title={t('tooltipSelectNoModels')} type="button">{t('selectNone')}</button>
        </span>
      </div>
      <div className="table-scroll import-models-scroll">
        <table className="data-table">
          <thead>
            <tr>
              <th className="checkbox-cell" />
              <th>{t('model')}</th>
              <th>{t('provider')}</th>
              <th>{t('size')}</th>
              <th>{t('serves')}</th>
            </tr>
          </thead>
          <tbody>
            {candidates.length === 0 ? (
              <tr><td className="empty-cell" colSpan={5}>{selectedProviders.length === 0 ? t('importPickProvider') : t('noProviderModels')}</td></tr>
            ) : candidates.map((model) => (
              <tr className={model.advertised ? 'muted-row' : ''} key={model.key}>
                <td className="checkbox-cell">
                  <input aria-label={t('importModelCheckbox', { model: model.name })} checked={model.advertised || !!picked[model.key]} disabled={model.advertised} onChange={(event) => setPicked((current) => ({ ...current, [model.key]: event.target.checked }))} type="checkbox" />
                </td>
                <td><code>{model.name}</code>{model.advertised ? <span className="status-chip success inline-chip">{t('advertised')}</span> : null}</td>
                <td>{model.provider}</td>
                <td>{formatSize(model.sizeBytes)}</td>
                <td>
                  <select disabled={model.advertised} onChange={(event) => setKinds((current) => ({ ...current, [model.key]: event.target.value }))} title={t('tooltipImportServes')} value={kinds[model.key] || 'completions'}>
                    <option value="completions">{t('completions')}</option>
                    <option value="embeddings">{t('embeddings')}</option>
                    <option value="both">{t('completionsAndEmbeddings')}</option>
                  </select>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </form>
  );
}

// ---------------------------------------------------------------------------------------------
// Create/edit forms: "Save" saves and closes; "Save and add another" saves and keeps the form open.
// ---------------------------------------------------------------------------------------------

/**
 * Wrap a create/edit form. onSave(formData) performs the save and returns a short label for the saved record.
 * clearOnAnother lists field names emptied after "Save and add another"; the first one gets focus.
 */
export function ManagedForm({ children, className = 'form-grid', clearOnAnother = [], id, onClose, onSave }) {
  const { t } = useTranslation();
  const [error, setError] = useState('');
  const [flash, setFlash] = useState('');
  const [saving, setSaving] = useState(false);

  async function submit(event) {
    event.preventDefault();
    if (saving) return;
    const formElement = event.currentTarget;
    const another = event.nativeEvent?.submitter?.value === 'another';
    setSaving(true);
    setError('');
    setFlash('');
    try {
      const label = await onSave(new FormData(formElement));
      if (!another) {
        onClose?.();
        return;
      }
      clearOnAnother.forEach((name) => {
        const field = formElement.elements.namedItem(name);
        if (field && 'value' in field) field.value = '';
      });
      const first = clearOnAnother.length > 0 ? formElement.elements.namedItem(clearOnAnother[0]) : null;
      if (first && typeof first.focus === 'function') first.focus();
      setFlash(t('savedAddAnother', { name: label || '' }));
    } catch (err) {
      setError(err.message || String(err));
    } finally {
      setSaving(false);
    }
  }

  return (
    <form className={className} id={id} onSubmit={submit}>
      {error ? <div className="settings-restart-banner error-state span-full" role="alert"><CircleAlert size={16} /><span>{error}</span></div> : null}
      {flash ? <div className="settings-restart-banner span-full" role="status"><CheckCircle2 size={16} /><span>{flash}</span></div> : null}
      {children}
    </form>
  );
}

/** Footer buttons for a ManagedForm. allowAnother shows "Save and add another" (create forms only). */
export function SaveActions({ allowAnother, formId, tooltip }) {
  const { t } = useTranslation();
  return (
    <>
      <button className="primary-button" form={formId} title={tooltip} type="submit" value="close">{t('save')}</button>
      {allowAnother ? <button className="action-button" form={formId} title={t('tooltipSaveAndAddAnother')} type="submit" value="another">{t('saveAndAddAnother')}</button> : null}
    </>
  );
}
