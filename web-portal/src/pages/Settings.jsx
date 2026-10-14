import React, { useEffect, useState } from 'react';
import { Alert, Box, FormControl, FormControlLabel, FormLabel, Paper, Radio, RadioGroup, Typography } from '@mui/material';
import api from '../api';
import { isAdmin } from '../auth';

export default function Settings() {
  const [format, setFormat] = useState('QrCode');
  const [message, setMessage] = useState(null);
  const canManage = isAdmin();

  useEffect(() => { api.get('/settings/barcode-format').then(({ data }) => setFormat(data.format)).catch(() => setMessage({ type: 'error', text: 'Etiket formatı yüklenemedi.' })); }, []);

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

  return <Box><Typography variant="h4" sx={{ mb: 3, fontWeight: 700 }}>Sistem Ayarları</Typography>
    {message && <Alert severity={message.type} sx={{ mb: 2 }}>{message.text}</Alert>}
    <Paper variant="outlined" sx={{ p: 3, maxWidth: 640 }}>
      <FormControl disabled={!canManage}><FormLabel>Etiket Format Tercihi</FormLabel><RadioGroup value={format} onChange={update}>
        <FormControlLabel value="QrCode" control={<Radio />} label="2D QR Kod (Varsayılan)" />
        <FormControlLabel value="Code128" control={<Radio />} label="1D Çizgili Barkod (Code 128)" />
      </RadioGroup></FormControl>
      {!canManage && <Typography variant="body2" color="text.secondary">Bu ayarı yalnızca Admin rolü değiştirebilir.</Typography>}
    </Paper>
  </Box>;
}
