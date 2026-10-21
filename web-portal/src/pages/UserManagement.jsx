import React, { useEffect, useState } from 'react';
import {
  Alert, Box, Button, Chip, Dialog, DialogActions, DialogContent, DialogContentText,
  DialogTitle, MenuItem, Paper, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography,
} from '@mui/material';
import api from '../api';
import { getStoredUser } from '../auth';
import { getLocalizedApiError } from '../apiError';
import { isSameUser, requiresRoleConfirmation } from '../domain/users';
import { USER_ROLES, validateCreateUser, validateResetPassword } from '../domain/userValidation';
import { useLanguage } from '../useLanguage';

const emptyForm = { username: '', password: '', role: 'Operator' };

export default function UserManagement() {
  const { t } = useLanguage();
  const currentUser = getStoredUser();
  const [users, setUsers] = useState([]);
  const [form, setForm] = useState(emptyForm);
  const [message, setMessage] = useState(null);
  const [loading, setLoading] = useState(false);
  const [resetTarget, setResetTarget] = useState(null);
  const [resetPassword, setResetPassword] = useState('');
  const [resetError, setResetError] = useState('');
  const [confirmAction, setConfirmAction] = useState(null);
  const [busyUserId, setBusyUserId] = useState(null);

  const load = async () => {
    const response = await api.get('/users');
    setUsers(response.data);
  };

  useEffect(() => {
    load().catch(() => setMessage({ type: 'error', text: t('userLoadFailed') }));
  }, [t]);

  const handleError = async (error, fallback) => {
    setMessage({ type: 'error', text: getLocalizedApiError(error, t, fallback) });
    await load().catch(() => {});
  };

  const create = async (event) => {
    event.preventDefault();
    const validationKey = validateCreateUser(form);
    if (validationKey) {
      setMessage({ type: 'error', text: t(validationKey) });
      return;
    }

    setLoading(true);
    try {
      await api.post('/users', form);
      setForm(emptyForm);
      setMessage({ type: 'success', text: t('userCreated') });
      await load();
    } catch (error) {
      await handleError(error, t('userCreateFailed'));
    } finally {
      setLoading(false);
    }
  };

  const updateRole = async (user, role) => {
    setBusyUserId(user.id);
    try {
      await api.put(`/users/${user.id}/role`, { role });
      setMessage({ type: 'success', text: t('userRoleUpdated') });
      await load();
    } catch (error) {
      await handleError(error, t('roleUpdateFailed'));
    } finally {
      setBusyUserId(null);
    }
  };

  const requestRoleChange = (user, role) => {
    if (user.role === role) return;
    if (requiresRoleConfirmation(user, role, currentUser)) {
      setConfirmAction({ type: 'role', user, role });
      return;
    }
    updateRole(user, role);
  };

  const updateActive = async (user, active) => {
    setBusyUserId(user.id);
    try {
      await api.put(`/users/${user.id}/active`, { active });
      setMessage({ type: 'success', text: t(active ? 'userActivated' : 'userDeactivated') });
      await load();
    } catch (error) {
      await handleError(error, t('userStatusUpdateFailed'));
    } finally {
      setBusyUserId(null);
    }
  };

  const remove = async (user) => {
    setBusyUserId(user.id);
    try {
      await api.delete(`/users/${user.id}`);
      setMessage({ type: 'success', text: t('userDeleted') });
      await load();
    } catch (error) {
      await handleError(error, t('userDeleteFailed'));
    } finally {
      setBusyUserId(null);
    }
  };

  const confirmSelectedAction = async () => {
    if (!confirmAction) return;
    const { type, user, role } = confirmAction;
    setConfirmAction(null);
    if (type === 'delete') await remove(user);
    else if (type === 'role') await updateRole(user, role);
    else await updateActive(user, false);
  };

  const openReset = (user) => {
    setResetTarget(user);
    setResetPassword('');
    setResetError('');
  };

  const closeReset = () => {
    setResetTarget(null);
    setResetPassword('');
    setResetError('');
  };

  const resetUserPassword = async () => {
    const validationKey = validateResetPassword(resetPassword);
    if (validationKey) {
      setResetError(t(validationKey));
      return;
    }

    setBusyUserId(resetTarget.id);
    try {
      await api.post(`/users/${resetTarget.id}/reset-password`, { newPassword: resetPassword });
      closeReset();
      setMessage({ type: 'success', text: t('passwordResetSuccess') });
      await load();
    } catch (error) {
      setResetError(getLocalizedApiError(error, t, t('passwordResetFailed')));
      await load().catch(() => {});
    } finally {
      setBusyUserId(null);
    }
  };

  const confirmIsRole = confirmAction?.type === 'role';
  const confirmTitle = confirmIsRole
    ? t('changeUserRoleTitle')
    : confirmAction?.type === 'delete' ? t('deleteUserTitle') : t('deactivateUserTitle');
  const confirmText = (confirmIsRole
    ? t('roleChangeConfirm')
    : confirmAction?.type === 'delete' ? t('deleteUserConfirm') : t('deactivateUserConfirm'))
    .replace('{username}', confirmAction?.user?.username || '')
    .replace('{role}', confirmAction?.role || '');
  const confirmButton = confirmIsRole
    ? t('changeRole')
    : confirmAction?.type === 'delete' ? t('delete') : t('deactivate');

  return <Box>
    <Typography variant="h4" sx={{ mb: 1, fontWeight: 600 }}>{t('userManagementTitle')}</Typography>
    <Typography variant="body2" sx={{ mb: 3, color: 'text.secondary' }}>{t('userManagementDescription')}</Typography>
    {message && <Alert severity={message.type} sx={{ mb: 2 }}>{message.text}</Alert>}

    <Paper component="form" onSubmit={create} sx={{ p: 3, mb: 3, display: 'flex', gap: 2, flexWrap: 'wrap' }}>
      <TextField required label={t('username')} autoComplete="off" value={form.username}
        onChange={(event) => setForm({ ...form, username: event.target.value })} />
      <TextField required type="password" label={t('password')} autoComplete="new-password" value={form.password}
        onChange={(event) => setForm({ ...form, password: event.target.value })} />
      <TextField required select label={t('role')} value={form.role} sx={{ minWidth: 160 }}
        onChange={(event) => setForm({ ...form, role: event.target.value })}>
        {USER_ROLES.map((role) => <MenuItem key={role} value={role}>{role}</MenuItem>)}
      </TextField>
      <Button type="submit" variant="contained" disabled={loading}>{t('createUser')}</Button>
    </Paper>

    <Paper>
      <Table>
        <TableHead>
          <TableRow>
            <TableCell>{t('username')}</TableCell>
            <TableCell>{t('role')}</TableCell>
            <TableCell>{t('status')}</TableCell>
            <TableCell>{t('actions')}</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {users.map((user) => {
            const isSelf = isSameUser(user, currentUser);
            const rowBusy = busyUserId === user.id;
            return <TableRow key={user.id}>
              <TableCell>{user.username}</TableCell>
              <TableCell>
                <TextField select size="small" value={user.role} sx={{ minWidth: 140 }}
                  disabled={rowBusy}
                  onChange={(event) => requestRoleChange(user, event.target.value)}>
                  {USER_ROLES.map((role) => <MenuItem key={role} value={role}>{role}</MenuItem>)}
                </TextField>
              </TableCell>
              <TableCell>
                <Chip size="small" color={user.active ? 'success' : 'default'}
                  label={user.active ? t('active') : t('inactive')} />
              </TableCell>
              <TableCell>
                <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
                  <Button size="small" disabled={rowBusy || (isSelf && user.active)}
                    title={isSelf && user.active ? t('selfActionDisabled') : undefined}
                    onClick={() => user.active
                      ? setConfirmAction({ type: 'deactivate', user })
                      : updateActive(user, true)}>
                    {user.active ? t('deactivate') : t('activate')}
                  </Button>
                  <Button size="small" disabled={rowBusy} onClick={() => openReset(user)}>
                    {t('resetPassword')}
                  </Button>
                  <Button size="small" color="error" disabled={rowBusy || isSelf}
                    title={isSelf ? t('selfActionDisabled') : undefined}
                    onClick={() => setConfirmAction({ type: 'delete', user })}>
                    {t('delete')}
                  </Button>
                </Box>
              </TableCell>
            </TableRow>;
          })}
        </TableBody>
      </Table>
    </Paper>

    <Dialog open={Boolean(resetTarget)} onClose={closeReset}>
      <DialogTitle>{t('resetUserPasswordTitle')}</DialogTitle>
      <DialogContent>
        <DialogContentText sx={{ mb: 2 }}>
          {resetTarget?.username}
        </DialogContentText>
        {resetError && <Alert severity="error" sx={{ mb: 2 }}>{resetError}</Alert>}
        <TextField autoFocus fullWidth required type="password" label={t('newPassword')}
          autoComplete="new-password" value={resetPassword}
          onChange={(event) => setResetPassword(event.target.value)} />
      </DialogContent>
      <DialogActions>
        <Button onClick={closeReset}>{t('cancel')}</Button>
        <Button variant="contained" onClick={resetUserPassword}>{t('resetPassword')}</Button>
      </DialogActions>
    </Dialog>

    <Dialog open={Boolean(confirmAction)} onClose={() => setConfirmAction(null)}>
      <DialogTitle>{confirmTitle}</DialogTitle>
      <DialogContent>
        <DialogContentText>{confirmText}</DialogContentText>
      </DialogContent>
      <DialogActions>
        <Button onClick={() => setConfirmAction(null)}>{t('cancel')}</Button>
        <Button color={confirmAction?.type === 'delete' ? 'error' : 'warning'} variant="contained"
          onClick={confirmSelectedAction}>
          {confirmButton}
        </Button>
      </DialogActions>
    </Dialog>
  </Box>;
}
