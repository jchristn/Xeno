import { jsonBody, localApi } from '../localApi.js';
import { chatRanges } from '../../chat/chatRanges.js';
import { errorMessage, readChatStream } from '../../chat/chatStream.js';

// Chat routes of the client's local API. Every call goes through localApi (token header, key normalization) except
// sending a message, which reads the server-sent event stream as it arrives.

export function listConversations(search = '') {
  const query = search ? `?search=${encodeURIComponent(search)}&limit=200` : '?limit=200';
  return localApi(`/api/v1/chat/conversations${query}`);
}

export function createConversation(body) {
  return localApi('/api/v1/chat/conversations', { method: 'POST', body: jsonBody(body) });
}

export function getConversation(id) {
  return localApi(`/api/v1/chat/conversations/${encodeURIComponent(id)}`);
}

export function updateConversation(id, body) {
  return localApi(`/api/v1/chat/conversations/${encodeURIComponent(id)}`, { method: 'PUT', body: jsonBody(body) });
}

export function deleteConversation(id) {
  return localApi(`/api/v1/chat/conversations/${encodeURIComponent(id)}`, { method: 'DELETE' });
}

export function listChatModels() {
  return localApi('/api/v1/chat/models');
}

export function prepareModel(model) {
  return localApi('/api/v1/chat/models/prepare', { method: 'POST', body: jsonBody({ model }) });
}

export function listChatTools() {
  return localApi('/api/v1/chat/tools');
}

export function cancelTurn(turnId) {
  return localApi(`/api/v1/chat/turns/${encodeURIComponent(turnId)}/cancel`, { method: 'POST', body: '{}' });
}

export function approveToolCall(turnId, toolCallId, allow) {
  return localApi(`/api/v1/chat/turns/${encodeURIComponent(turnId)}/tool-calls/${encodeURIComponent(toolCallId)}/approval`, { method: 'POST', body: jsonBody({ allow }) });
}

export function setFeedback(messageId, rating, comment) {
  return localApi(`/api/v1/chat/messages/${encodeURIComponent(messageId)}/feedback`, { method: 'PUT', body: jsonBody({ rating, comment: comment || null }) });
}

export function deleteFeedback(messageId) {
  return localApi(`/api/v1/chat/messages/${encodeURIComponent(messageId)}/feedback`, { method: 'DELETE' });
}

export function queryHistory(filters) {
  return localApi(`/api/v1/chat/history${queryString(filters)}`);
}

export function getTurn(id) {
  return localApi(`/api/v1/chat/history/${encodeURIComponent(id)}`);
}

export function deleteHistory(filters) {
  return localApi(`/api/v1/chat/history${queryString(filters)}`, { method: 'DELETE' });
}

export function queryFeedback(filters) {
  return localApi(`/api/v1/chat/feedback${queryString(filters)}`);
}

export function chatAnalytics(fromUtc, toUtc, bucketSeconds) {
  return localApi(`/api/v1/chat/analytics${queryString({ fromUtc, toUtc, bucketSeconds })}`);
}

// Send a message and call onEvent for each streamed event ({ kind, text, turnId, messageId, toolCall, turn }).
// Resolves when the stream ends. Errors before the stream starts reject with the server's message.
export async function sendMessage(conversationId, body, onEvent, signal) {
  const token = window.__XENO_LOCAL_TOKEN__ || '';
  const response = await fetch(`/api/v1/chat/conversations/${encodeURIComponent(conversationId)}/messages`, {
    method: 'POST',
    cache: 'no-store',
    headers: {
      Accept: 'text/event-stream',
      'Accept-Language': navigator.language || 'en',
      'Content-Type': 'application/json',
      'X-Xeno-Local-Token': token
    },
    body: jsonBody(body),
    signal
  });
  if (!response.ok) throw new Error(await errorMessage(response));
  await readChatStream(response, onEvent);
}

function queryString(values) {
  const parts = Object.entries(values || {})
    .filter(([, value]) => value !== undefined && value !== null && value !== '')
    .map(([key, value]) => `${encodeURIComponent(key)}=${encodeURIComponent(value)}`);
  return parts.length ? `?${parts.join('&')}` : '';
}

// Turn a dashboard range key into an analytics window.
export function rangeWindow(range) {
  const { hours, bucketSeconds } = chatRanges[range] || chatRanges.lastDay;
  const to = new Date();
  const from = new Date(to.getTime() - hours * 3600 * 1000);
  return { fromUtc: from.toISOString(), toUtc: to.toISOString(), bucketSeconds };
}
