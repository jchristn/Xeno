// Shared helpers for chat APIs: camelCase keys from PascalCase server JSON, and read a chat event stream.

export function camelKeys(value) {
  if (Array.isArray(value)) return value.map(camelKeys);
  if (!value || typeof value !== 'object') return value;
  const result = {};
  Object.entries(value).forEach(([key, entry]) => {
    result[key.charAt(0).toLowerCase() + key.slice(1)] = camelKeys(entry);
  });
  return result;
}

export async function errorMessage(response) {
  const text = await response.text();
  try {
    const parsed = JSON.parse(text);
    return parsed.Message || parsed.message || parsed.error || text || response.statusText;
  } catch {
    return text || response.statusText;
  }
}

// Read server-sent chat events from a fetch response and call onEvent with each, camelCased.
export async function readChatStream(response, onEvent) {
  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = '';
  for (;;) {
    const { done, value } = await reader.read();
    if (done) break;
    buffer += decoder.decode(value, { stream: true });
    buffer = drain(buffer, onEvent);
  }
  drain(`${buffer}\n\n`, onEvent);
}

function drain(text, onEvent) {
  let buffer = text.replace(/\r\n/g, '\n');
  let end = buffer.indexOf('\n\n');
  while (end >= 0) {
    const block = buffer.slice(0, end);
    buffer = buffer.slice(end + 2);
    const data = block.split('\n').filter((line) => line.startsWith('data:')).map((line) => line.slice(5).trimStart()).join('');
    if (data) {
      try {
        onEvent(camelKeys(JSON.parse(data)));
      } catch {
        // Skip an unreadable event.
      }
    }
    end = buffer.indexOf('\n\n');
  }
  return buffer;
}
