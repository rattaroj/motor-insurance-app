import { configureStore } from '@reduxjs/toolkit';
import { setupListeners } from '@reduxjs/toolkit/query';
import { useDispatch, useSelector, type TypedUseSelectorHook } from 'react-redux';
import { insuranceApi } from '../api/insuranceApi';
import { authReducer } from '../auth/authSlice';

export const makeStore = () =>
  configureStore({
    reducer: {
      [insuranceApi.reducerPath]: insuranceApi.reducer,
      auth: authReducer,
    },
    middleware: (getDefault) => getDefault().concat(insuranceApi.middleware),
  });

export const store = makeStore();
setupListeners(store.dispatch);

export type AppStore = ReturnType<typeof makeStore>;
export type RootState = ReturnType<AppStore['getState']>;
export type AppDispatch = AppStore['dispatch'];

export const useAppDispatch: () => AppDispatch = useDispatch;
export const useAppSelector: TypedUseSelectorHook<RootState> = useSelector;
