import React, { useEffect, useRef, useState } from 'react';
import { Alert, Box, Button, Paper, Stack, TextField, Typography } from '@mui/material';
import { useNavigate } from 'react-router-dom';
import api from '../api';
import { clearSession, currentUsername } from '../auth';
import { getLocalizedApiError } from '../apiError';
import { validatePasswordChange } from '../domain/passwordValidation';
import { useLanguage } from '../useLanguage';

const emptyForm = { currentPassword: '', newPassword: '', confirmPassword: '' };

export default function ChangePassword() {
  const { t } = useLanguage();
  const navigate = useNavigate();
  const username = currentUsername();
  const [form, setForm] = useState(emptyForm);
  const [message, setMessage] = useState(null);
  const [loading, setLoading] = useState(false);
  const redirectTimer = useRef(null);

  useEffect(() => () => {
    if (redirectTimer.current) clearTimeout(redirectTimer.current);
    setForm(emptyForm);
  }, []);

  const updateField = (field) => (event) => {
    setForm((current) => ({ ...current, [field]: event.target.value }));
    setMessage(null);
  };

  const submit = async (event) => {
    event.preventDefault();
    const validationKey = validatePasswordChange(form);
    if (validationKey) {
      setMessage({ type: 'error', text: t(validationKey) });
      return;
    }

    if (!username) {
      setMessage({ type: 'error', text: t('loginFailed') });
      return;
    }

    setLoading(true);
    setMessage(null);
    try {
      await api.post('/auth/change-password', {
        username,
        currentPassword: form.currentPassword,
        newPassword: form.newPassword,
      });
      setForm(emptyForm);
      setMessage({ type: 'success', text: t('passwordChangeSuccess') });
      redirectTimer.current = setTimeout(() => {
        clearSession();
        navigate('/login', { replace: true });
      }, 1200);
    } catch (error) {
      setMessage({
        type: 'error',
        text: getLocalizedApiError(error, t, t('passwordChangeFailed')),
      });
    } finally {
      setLoading(false);
    }
  };

  return (
    <Box sx={{ maxWidth: 560 }}>
      <Typography variant="h4" sx={{ mb: 1, fontWeight: 600 }}>{t('changePasswordTitle')}</Typography>
      <Typography variant="body2" sx={{ mb: 3, color: 'text.secondary' }}>
        {t('changePasswordDescription')}
      </Typography>

      {message && <Alert severity={message.type} sx={{ mb: 2 }}>{message.text}</Alert>}

      <Paper component="form" onSubmit={submit} sx={{ p: 3 }}>
        <input
          type="text"
          name="username"
          autoComplete="username"
          value={username}
          readOnly
          hidden
        />
        <Stack spacing={2}>
          <TextField
            required
            fullWidth
            type="password"
            name="current-password"
            label={t('currentPassword')}
            autoComplete="current-password"
            value={form.currentPassword}
            onChange={updateField('currentPassword')}
          />
          <TextField
            required
            fullWidth
            type="password"
            name="new-password"
            label={t('newPassword')}
            autoComplete="new-password"
            helperText={t('passwordRulesHint')}
            value={form.newPassword}
            onChange={updateField('newPassword')}
          />
          <TextField
            required
            fullWidth
            type="password"
            name="confirm-new-password"
            label={t('confirmNewPassword')}
            autoComplete="new-password"
            value={form.confirmPassword}
            onChange={updateField('confirmPassword')}
          />
          <Box sx={{ display: 'flex', gap: 1.5 }}>
            <Button type="submit" variant="contained" disabled={loading}>
              {loading ? t('updatingPassword') : t('updatePassword')}
            </Button>
            <Button variant="outlined" onClick={() => navigate('/dashboard')} disabled={loading}>
              {t('cancel')}
            </Button>
          </Box>
        </Stack>
      </Paper>
    </Box>
  );
}
