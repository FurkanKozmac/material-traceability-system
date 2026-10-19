import React, { useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  FormControl,
  FormControlLabel,
  FormLabel,
  Paper,
  Radio,
  RadioGroup,
  Switch,
  TextField,
  Typography,
} from '@mui/material';
import api from '../api';
import { isAdmin } from '../auth';
import { useLanguage } from '../useLanguage';

export default function Settings() {
  const { t } = useLanguage();
  const [format, setFormat] = useState('QrCode');
  const [message, setMessage] = useState(null);
  const [recipient, setRecipient] = useState('alerts@mts.local');
  const [enabled, setEnabled] = useState(true);
  const [savingNotifications, setSavingNotifications] = useState(false);
  const [sendingTest, setSendingTest] = useState(false);
  const canManage = isAdmin();

  useEffect(() => {
    api.get('/settings/barcode-format')
      .then(({ data }) => setFormat(data.format))
      .catch(() => setMessage({ type: 'error', text: t('labelFormatLoadFailed') }));
    api.get('/settings/notifications')
      .then(({ data }) => {
        setRecipient(data.alertEmailRecipient);
        setEnabled(data.enableLowStockAlerts);
      })
      .catch(() => setMessage({ type: 'error', text: t('notificationSettingsLoadFailed') }));
  }, [t]);

  const update = async (event) => {
    const next = event.target.value;
    const previous = format;
    setFormat(next);
    setMessage(null);
    try {
      const { data } = await api.put('/settings/barcode-format', { format: next });
      setFormat(data.format);
      setMessage({ type: 'success', text: t('labelFormatUpdated') });
    } catch {
      setFormat(previous);
      setMessage({ type: 'error', text: t('labelFormatUpdateFailed') });
    }
  };

  const saveNotifications = async () => {
    setSavingNotifications(true);
    setMessage(null);
    try {
      const { data } = await api.put('/settings/notifications', {
        alertEmailRecipient: recipient,
        enableLowStockAlerts: enabled,
      });
      setRecipient(data.alertEmailRecipient);
      setEnabled(data.enableLowStockAlerts);
      setMessage({ type: 'success', text: t('notificationSettingsSaved') });
    } catch (error) {
      setMessage({
        type: 'error',
        text: t('notificationSettingsSaveFailed'),
      });
    } finally {
      setSavingNotifications(false);
    }
  };

  const sendTest = async () => {
    setSendingTest(true);
    setMessage(null);
    try {
      await api.post('/settings/test-alert-email');
      setMessage({ type: 'success', text: t('testEmailSent') });
    } catch {
      setMessage({ type: 'error', text: t('testEmailFailed') });
    } finally {
      setSendingTest(false);
    }
  };

  return (
    <Box>
      <Typography variant="h4" sx={{ mb: 3, fontWeight: 700 }}>{t('settingsTitle')}</Typography>
      {message && <Alert severity={message.type} sx={{ mb: 2 }}>{message.text}</Alert>}
      <Paper variant="outlined" sx={{ p: 3, maxWidth: 640 }}>
        <FormControl disabled={!canManage}>
          <FormLabel>{t('labelFormatPreference')}</FormLabel>
          <RadioGroup value={format} onChange={update}>
            <FormControlLabel value="QrCode" control={<Radio />} label={t('qrCodeDefault')} />
            <FormControlLabel value="Code128" control={<Radio />} label={t('code128Barcode')} />
          </RadioGroup>
        </FormControl>
        {!canManage && <Typography variant="body2" color="text.secondary">{t('adminOnlyLabelFormat')}</Typography>}
      </Paper>
      <Paper variant="outlined" sx={{ p: 3, maxWidth: 640, mt: 3 }}>
        <Typography variant="h6" sx={{ mb: 2 }}>{t('criticalStockSettings')}</Typography>
        <TextField
          fullWidth
          type="email"
          label={t('notificationEmail')}
          value={recipient}
          onChange={(event) => setRecipient(event.target.value)}
          disabled={!canManage}
          sx={{ mb: 1 }}
        />
        <FormControlLabel
          control={<Switch checked={enabled} onChange={(event) => setEnabled(event.target.checked)} disabled={!canManage} />}
          label={t('enableLowStockAlerts')}
        />
        <Box sx={{ display: 'flex', gap: 1, mt: 2 }}>
          <Button variant="contained" onClick={saveNotifications} disabled={!canManage || savingNotifications}>
            {savingNotifications ? t('savingSettings') : t('saveSettings')}
          </Button>
          <Button variant="outlined" onClick={sendTest} disabled={!canManage || sendingTest}>
            {sendingTest ? t('sendingTestEmail') : t('sendTestEmail')}
          </Button>
        </Box>
        {!canManage && <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>{t('adminOnlyNotifications')}</Typography>}
      </Paper>
    </Box>
  );
}
