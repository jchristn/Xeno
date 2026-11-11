import { createContext, useContext } from 'react';

// The chat page talks to whichever chat API its dashboard provides: the client's local API in the local dashboard,
// the Xeno server's API in the server dashboard. Each provides the same functions; supportsTools says whether the
// tools switch applies.
export const ChatApiContext = createContext(null);

export function useChatApi() {
  const value = useContext(ChatApiContext);
  if (!value) throw new Error('ChatApiContext is missing.');
  return value;
}
