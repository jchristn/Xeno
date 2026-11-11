import { createContext, useContext } from 'react';

// The usage API the shared Usage page calls: the local dashboard's or the server's.
export const UsageApiContext = createContext(null);

export function useUsageApi() {
  return useContext(UsageApiContext);
}
