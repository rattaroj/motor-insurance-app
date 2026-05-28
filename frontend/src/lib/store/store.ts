import { configureStore } from '@reduxjs/toolkit';
import { setupListeners } from '@reduxjs/toolkit/query';
import { insuranceApi } from '../api/insuranceApi';

export const makeStore = () =>
  configureStore({
    reducer: { [insuranceApi.reducerPath]: insuranceApi.reducer },
    middleware: (getDefault) => getDefault().concat(insuranceApi.middleware),
  });

export const store = makeStore();
setupListeners(store.dispatch);

export type AppStore = ReturnType<typeof makeStore>;
export type RootState = ReturnType<AppStore['getState']>;
export type AppDispatch = AppStore['dispatch'];
