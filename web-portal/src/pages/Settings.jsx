import React, { useEffect, useState } from 'react';
import { Alert, Box, Button, FormControl, FormControlLabel, FormLabel, Paper, Radio, RadioGroup, Switch, TextField, Typography } from '@mui/material';
import api from '../api';
import { isAdmin } from '../auth';

export default function Settings() {
  const [format, setFormat] = useState('QrCode');
  const [message, setMessage] = useState(null);
  const [recipient, setRecipient] = useState('alerts@mts.local');
  const [enabled, setEnabled] = useState(true);
  const [savingNotifications, setSavingNotifications] = useState(false);
  const [sendingTest, setSendingTest] = useState(false);
  const canManage = isAdmin();

  useEffect(() => {
    api.get('/settings/barcode-format').then(({ data }) => setFormat(data.format)).catch(() => setMessage({ type: 'error', text: 'Etiket formatı yüklenemedi.' }));
    api.get('/settings/notifications').then(({ data }) => { setRecipient(data.alertEmailRecipient); setEnabled(data.enableLowStockAlerts); }).catch(() => setMessage({ type: 'error', text: 'Bildirim ayarları yüklenemedi.' }));
  }, []);

  const update = async (event) => {
    const next = event.target.value;
    const previous = format;
    setFormat(next);
    setMessage(null);
    try {
      const { data } = await api.put('/settings/barcode-format', { format: next });
      setFormat(data.format);
      setMessage({ type: 'success', text: 'Etiket formatı güncellendi.' });
    } catch {
      setFormat(previous);
      setMessage({ type: 'error', text: 'Etiket formatı güncellenemedi.' });
    }
  };

  const saveNotifications = async () => {
    setSavingNotifications(true); setMessage(null);
    try {
      const { data } = await api.put('/settings/notifications', { alertEmailRecipient: recipient, enableLowStockAlerts: enabled });
      setRecipient(data.alertEmailRecipient); setEnabled(data.enableLowStockAlerts);
      setMessage({ type: 'success', text: 'Kritik stok bildirim ayarları kaydedildi.' });
    } catch (error) { setMessage({ type: 'error', text: error.response?.data?.message || 'Bildirim ayarları kaydedilemedi.' }); }
    finally { setSavingNotifications(false); }
  };

  const sendTest = async () => {
    setSendingTest(true); setMessage(null);
    try { await api.post('/settings/test-alert-email'); setMessage({ type: 'success', text: 'Test e-postası gönderildi. Mailpit gelen kutusunu kontrol edin.' }); }
    catch { setMessage({ type: 'error', text: 'Test e-postası gönderilemedi.' }); }
    finally { setSendingTest(false); }
  };

  return <Box><Typography variant="h4" sx={{ mb: 3, fontWeight: 700 }}>Sistem Ayarları</Typography>
    {message && <Alert severity={message.type} sx={{ mb: 2 }}>{message.text}</Alert>}
    <Paper variant="outlined" sx={{ p: 3, maxWidth: 640 }}>
      <FormControl disabled={!canManage}><FormLabel>Etiket Format Tercihi</FormLabel><RadioGroup value={format} onChange={update}>
        <FormControlLabel value="QrCode" control={<Radio />} label="2D QR Kod (Varsayılan)" />
        <FormControlLabel value="Code128" control={<Radio />} label="1D Çizgili Barkod (Code 128)" />
      </RadioGroup></FormControl>
      {!canManage && <Typography variant="body2" color="text.secondary">Bu ayarı yalnızca Admin rolü değiştirebilir.</Typography>}
    </Paper>
    <Paper variant="outlined" sx={{ p: 3, maxWidth: 640, mt: 3 }}>
      <Typography variant="h6" sx={{ mb: 2 }}>Kritik Stok ve Bildirim Ayarları</Typography>
      <TextField fullWidth type="email" label="Bildirim E-posta Adresi" value={recipient} onChange={(event) => setRecipient(event.target.value)} disabled={!canManage} sx={{ mb: 1 }} />
      <FormControlLabel control={<Switch checked={enabled} onChange={(event) => setEnabled(event.target.checked)} disabled={!canManage} />} label="Düşük Stok Uyarılarını Etkinleştir" />
      <Box sx={{ display: 'flex', gap: 1, mt: 2 }}>
        <Button variant="contained" onClick={saveNotifications} disabled={!canManage || savingNotifications}>{savingNotifications ? 'Kaydediliyor…' : 'Ayarları Kaydet'}</Button>
        <Button variant="outlined" onClick={sendTest} disabled={!canManage || sendingTest}>{sendingTest ? 'Gönderiliyor…' : 'Test E-postası Gönder'}</Button>
      </Box>
      {!canManage && <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>Bildirim ayarlarını yalnızca Admin rolü değiştirebilir.</Typography>}
    </Paper>
  </Box>;
}
