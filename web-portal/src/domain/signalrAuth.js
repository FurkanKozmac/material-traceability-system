export const isUnauthorizedHubError = (error) => {
  const message = typeof error?.message === 'string' ? error.message : '';
  return error?.statusCode === 401 ||
    error?.response?.status === 401 ||
    /401/.test(message) ||
    /unauthorized/i.test(message);
};

export const shouldRetryClosedHub = (error, alreadyRetried) =>
  !alreadyRetried && isUnauthorizedHubError(error);

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
