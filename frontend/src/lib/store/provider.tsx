'use client';
import { Provider } from 'react-redux';
import { store } from './store';
import { AuthProvider } from '@/components/auth-provider';

export function StoreProvider({ children }: { children: React.ReactNode }) {
  return (
    <Provider store={store}>
      <AuthProvider>{children}</AuthProvider>
    </Provider>
  );
}
