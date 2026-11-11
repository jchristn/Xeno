# Local and Xeno Network Models Together

The Xeno client's local endpoint gives apps on your machine one address for two kinds of models:

- **Local models** run on your own machine (Ollama, vLLM, OpenAI-compatible). Requests go straight to them.
- **Network models** are served by other machines on the Xeno network. Requests go through the Xeno server.

## 1. Turn on the local endpoint

In `~/.xeno/client.json`:

```json
"LocalInferenceServer": {
  "Enabled": true,
  "Port": 11435,
  "RoutingMode": "PreferLocal"
}
```

Ollama keeps port 11434. Xeno listens on 11435.

## 2. Add your local provider and models

```bash
xeno provider add --name local-ollama --type Ollama --endpoint http://127.0.0.1:11434
xeno model add --provider local-ollama --name llama3.1:8b --type completion
```

Or add them to the same `~/.xeno/client.json` (restart the client after editing):

```json
"Providers": [
  {
    "Name": "local-ollama",
    "ApiType": "Ollama",
    "Endpoint": "http://127.0.0.1:11434",
    "ShareWithNetwork": true,
    "Models": [
      { "Name": "llama3.1:8b", "SupportsCompletions": true }
    ]
  }
]
```

To keep a provider for your own use and never share it with the network (for example a paid cloud account), add `--local-only` or set `"ShareWithNetwork": false`.

## 3. Start the client and point your apps at it

```bash
xeno start
```

- OpenAI base URL: `http://127.0.0.1:11435/v1`
- Ollama base URL: `http://127.0.0.1:11435` (Ollama CLI: `OLLAMA_HOST=127.0.0.1:11435`)

`/v1/models` and `/api/tags` list local models first, then network models.

## How requests are routed

| Model | Goes to |
|---|---|
| Served by one of your providers | That provider, directly |
| Anything else | The Xeno network |

Each response's `X-Xeno-Route` header tells you where it went (`local:<provider>` or `network`).

To see how much traffic went each way, open the Usage page and choose the Local endpoint dataset, or run `xeno usage --dataset endpoint --group-by route`. Requests that fell back to the network appear as their own series. See [USAGE_ANALYTICS.md](USAGE_ANALYTICS.md).

## Override one model

```bash
xeno inference route set llama3.1:8b --local local-ollama --fallback   # local; use the network if it fails before responding
xeno inference route set qwen3:235b --network                         # always use the network
xeno inference resolve llama3.1:8b                                    # where a model goes, and why
```

## Tips

- **Concurrency:** each provider allows 4 requests at a time by default, shared between your apps and the network. Change it with `xeno inference concurrency <n>`. A local request that waits too long for a slot gets a `429`.
- **Gateways:** if your provider is a gateway whose model list includes models it will not serve, set `"DiscoverLocalModels": false` so only the models you configured are routed locally.
- **Checking traffic:** open the **Local endpoint** page in the desktop app, the terminal app (`xeno tui`), or the local dashboard, or run `xeno inference backends --check`.
