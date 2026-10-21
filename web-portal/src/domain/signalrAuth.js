export const isUnauthorizedHubError = (error) =>
  error?.statusCode === 401 || error?.response?.status === 401;

export async function startHubWithSingleRefresh({
  start, refresh, clearSession, redirectToLogin,
}) {
  try {
    await start();
    return 'connected';
  } catch (error) {
    if (!isUnauthorizedHubError(error)) return 'failed';

    try {
      await refresh();
    } catch {
      clearSession();
      redirectToLogin();
      return 'logged-out';
    }

    try {
      await start();
      return 'connected';
    } catch (retryError) {
      if (isUnauthorizedHubError(retryError)) {
        clearSession();
        redirectToLogin();
        return 'logged-out';
      }
      return 'failed';
    }
  }
}
