import { resources } from '../src/i18n/resources.js';

const baseKeys = Object.keys(resources.en.translation).sort();
let failures = 0;

for (const [locale, resource] of Object.entries(resources)) {
  const keys = Object.keys(resource.translation).sort();
  const missing = baseKeys.filter((key) => !keys.includes(key));
  const extra = keys.filter((key) => !baseKeys.includes(key));

  if (missing.length > 0 || extra.length > 0) {
    failures += 1;
    console.error(`${locale}: missing=${missing.join(',')} extra=${extra.join(',')}`);
  }
}

if (failures > 0) {
  process.exit(1);
}

console.log(`i18n-ok locales=${Object.keys(resources).length} keys=${baseKeys.length}`);
