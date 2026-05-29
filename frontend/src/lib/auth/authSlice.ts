import { createSlice, type PayloadAction } from '@reduxjs/toolkit';

export interface UserProfile {
  id: number;
  username: string;
  fullName: string;
  email: string;
  roles: string[];
  permissions: string[];
}

/** Shape returned by POST /auth/login and /auth/refresh (after envelope unwrap). */
export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  user: UserProfile;
}

export type AuthStatus = 'idle' | 'authenticated' | 'unauthenticated';

interface AuthState {
  user: UserProfile | null;
  accessToken: string | null;
  status: AuthStatus;
}

// Access token lives in memory only (never localStorage); it is re-bootstrapped from the
// httpOnly refresh cookie on page load via a silent /auth/refresh call.
const initialState: AuthState = {
  user: null,
  accessToken: null,
  status: 'idle',
};

const authSlice = createSlice({
  name: 'auth',
  initialState,
  reducers: {
    setCredentials(state, action: PayloadAction<AuthResponse>) {
      state.user = action.payload.user;
      state.accessToken = action.payload.accessToken;
      state.status = 'authenticated';
    },
    clearCredentials(state) {
      state.user = null;
      state.accessToken = null;
      state.status = 'unauthenticated';
    },
  },
});

export const { setCredentials, clearCredentials } = authSlice.actions;
export const authReducer = authSlice.reducer;
