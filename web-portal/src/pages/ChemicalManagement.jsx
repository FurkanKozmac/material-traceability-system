import React, { useEffect, useState } from 'react';
import { Alert, Box, Button, MenuItem, Paper, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography } from '@mui/material';
import api from '../api';
import { isAdmin } from '../auth';

const storageTypes = [
  { value: 'GENERAL', label: 'Genel' },
  { value: 'FLAMMABLE', label: 'Yanıcı' },
  { value: 'CORROSIVE', label: 'Korozif' },
  { value: 'PAINT', label: 'Boya' },
];
const storageTypeLabel = (value) => storageTypes.find((item) => item.value === value)?.label || value;
const emptyForm = { chemicalCode: '', name: '', description: '', msdsUrl: '', storageType: 'GENERAL' };

export default function ChemicalManagement() {
  const canManage = isAdmin();
  const [chemicals, setChemicals] = useState([]);
  const [form, setForm] = useState(emptyForm);
  const [message, setMessage] = useState(null);
  const load = async () => setChemicals((await api.get('/chemicals')).data);

  useEffect(() => { load().catch(() => setMessage({ type: 'error', text: 'Kimyasal kataloğu yüklenemedi.' })); }, []);

  const create = async (event) => {
    event.preventDefault();
    try {
      await api.post('/chemicals', { ...form, description: form.description || null, msdsUrl: form.msdsUrl || null });
      setForm(emptyForm);
      setMessage({ type: 'success', text: 'Kimyasal kaydı oluşturuldu.' });
      await load();
    } catch (error) {
      setMessage({ type: 'error', text: error.response?.data?.message || error.response?.data || 'Kimyasal kaydı oluşturulamadı.' });
    }
  };

  const remove = async (chemical) => {
    if (!window.confirm(`${chemical.chemicalCode} kodlu kimyasal silinsin mi?`)) return;
    try {
      await api.delete(`/chemicals/${chemical.id}`);
      setMessage({ type: 'success', text: 'Kimyasal kaydı silindi.' });
      await load();
    } catch (error) {
      setMessage({ type: 'error', text: error.response?.data?.message || error.response?.data || 'Kimyasal kaydı silinemedi.' });
    }
  };

  const updateStorageType = async (chemical, storageType) => {
    try {
      await api.put(`/chemicals/${chemical.id}`, {
        name: chemical.name,
        description: chemical.description,
        msdsUrl: chemical.msdsUrl,
        stopped: chemical.stopped,
        storageType,
      });
      setMessage({ type: 'success', text: `${chemical.chemicalCode} depolama türü güncellendi.` });
      await load();
    } catch (error) {
      setMessage({ type: 'error', text: error.response?.data?.message || 'Depolama türü güncellenemedi.' });
    }
  };

  return <Box>
    <Typography variant="h4" sx={{ mb: 3, fontWeight: 600 }}>Kimyasal Kataloğu</Typography>
    {message && <Alert severity={message.type} sx={{ mb: 2 }}>{String(message.text)}</Alert>}
    {canManage && <Paper component="form" onSubmit={create} sx={{ p: 3, mb: 3, display: 'grid', gridTemplateColumns: { xs: '1fr', md: '1fr 2fr 1.5fr 2fr 2fr auto' }, gap: 2 }}>
      <TextField required label="Kimyasal Kodu" value={form.chemicalCode} onChange={(e) => setForm({ ...form, chemicalCode: e.target.value })} />
      <TextField required label="Kimyasal Adı" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
      <TextField required select label="Depolama Türü" value={form.storageType} onChange={(e) => setForm({ ...form, storageType: e.target.value })}>{storageTypes.map((item) => <MenuItem key={item.value} value={item.value}>{item.label}</MenuItem>)}</TextField>
      <TextField label="Açıklama" value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
      <TextField label="MSDS URL" value={form.msdsUrl} onChange={(e) => setForm({ ...form, msdsUrl: e.target.value })} />
      <Button type="submit" variant="contained">Kaydet / Oluştur</Button>
    </Paper>}
    <Paper><Table><TableHead><TableRow><TableCell>Kod</TableCell><TableCell>Ad</TableCell><TableCell>Depolama Türü</TableCell><TableCell>Açıklama</TableCell><TableCell>Durum</TableCell><TableCell /></TableRow></TableHead>
      <TableBody>{chemicals.map((chemical) => <TableRow key={chemical.id}><TableCell>{chemical.chemicalCode}</TableCell><TableCell>{chemical.name}</TableCell><TableCell><TextField select size="small" disabled={!canManage} value={chemical.storageType} onChange={(e) => updateStorageType(chemical, e.target.value)} sx={{ minWidth: 130 }}>{storageTypes.map((item) => <MenuItem key={item.value} value={item.value}>{item.label}</MenuItem>)}</TextField></TableCell><TableCell>{chemical.description || '-'}</TableCell><TableCell>{chemical.stopped ? 'Durduruldu' : 'Aktif'}</TableCell><TableCell align="right">{canManage && <Button color="error" onClick={() => remove(chemical)}>Sil</Button>}</TableCell></TableRow>)}</TableBody>
    </Table></Paper>
  </Box>;
}
