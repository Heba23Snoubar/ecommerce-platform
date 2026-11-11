import { createContext, useContext, useEffect, useMemo, useState } from "react";
import { storeApi } from "../services/storeApi";
import { storage } from "../utils/storage";

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [user, setUser] = useState(() => storage.get("shopflow_user"));
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (user) {
      storage.set("shopflow_user", user);
    } else {
      storage.remove("shopflow_user");
    }
  }, [user]);

  // Synchronize or validate current session on initial page load if token is present
  useEffect(() => {
    const token = storage.get("shopflow_token");
    if (token) {
      storeApi
        .getMe()
        .then((freshUser) => {
          if (freshUser) setUser(freshUser);
        })
        .catch((err) => {
          if (err?.status === 401 || err?.status === 403) {
            storage.remove("shopflow_token");
            storage.remove("shopflow_refresh_token");
            storage.remove("shopflow_user");
            setUser(null);
          }
        });
    }
  }, []);

  const login = async (credentials) => {
    setLoading(true);
    try {
      const result = await storeApi.login(credentials);
      storage.set("shopflow_token", result.token);
      setUser(result.user);
      return result.user;
    } finally {
      setLoading(false);
    }
  };

  const register = async (data) => {
    setLoading(true);
    try {
      const result = await storeApi.register(data);
      storage.set("shopflow_token", result.token);
      setUser(result.user);
      return result.user;
    } finally {
      setLoading(false);
    }
  };

  const logout = async () => {
    try {
      await storeApi.logout();
    } catch {
      // ignore network errors on logout
    } finally {
      storage.remove("shopflow_token");
      storage.remove("shopflow_refresh_token");
      storage.remove("shopflow_user");
      setUser(null);
    }
  };

  const value = useMemo(
    () => ({
      user,
      loading,
      login,
      register,
      logout,
      isAdmin: user?.role === "Admin"
    }),
    [user, loading]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export const useAuth = () => useContext(AuthContext);
