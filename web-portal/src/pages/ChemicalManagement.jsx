import React, { useEffect, useState } from 'react';
import { Alert, Box, Button, MenuItem, Paper, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography } from '@mui/material';
import api from '../api';
import { isAdmin } from '../auth';
import { useLanguage } from '../useLanguage';

const emptyForm = { chemicalCode: '', name: '', description: '', msdsUrl: '', storageType: 'GENERAL' };

export default function ChemicalManagement() {
  const { t } = useLanguage();
  const canManage = isAdmin();
  const [chemicals, setChemicals] = useState([]);
  const [form, setForm] = useState(emptyForm);
  const [message, setMessage] = useState(null);
  const storageTypes = [
    { value: 'GENERAL', label: t('general') },
    { value: 'SOLVENT', label: t('solvent') },
    { value: 'FLAMMABLE', label: t('flammable') },
    { value: 'CORROSIVE', label: t('corrosive') },
    { value: 'PAINT', label: t('paint') },
  ];
  const load = async () => setChemicals((await api.get('/chemicals')).data);

  useEffect(() => { load().catch(() => setMessage({ type: 'error', text: t('chemicalLoadFailed') })); }, [t]);

  const create = async (event) => {
    event.preventDefault();
    try {
      await api.post('/chemicals', { ...form, description: form.description || null, msdsUrl: form.msdsUrl || null });
      setForm(emptyForm);
      setMessage({ type: 'success', text: t('chemicalCreated') });
      await load();
    } catch (error) {
      setMessage({ type: 'error', text: error.response?.data?.message || error.response?.data || t('chemicalCreateFailed') });
    }
  };

  const remove = async (chemical) => {
    if (!window.confirm(`${chemical.chemicalCode} ${t('deleteChemicalConfirm')}`)) return;
    try {
      await api.delete(`/chemicals/${chemical.id}`);
      setMessage({ type: 'success', text: t('chemicalDeleted') });
      await load();
    } catch (error) {
      setMessage({ type: 'error', text: error.response?.data?.message || error.response?.data || t('chemicalDeleteFailed') });
    }
  };

  const updateStorageType = async (chemical, storageType) => {
    const previousStorageType = chemical.storageType || 'GENERAL';
    setChemicals((previousChemicals) => previousChemicals.map((item) => (
      item.id === chemical.id ? { ...item, storageType } : item
    )));

    try {
      await api.put(`/chemicals/${chemical.id}`, {
        name: chemical.name,
        description: chemical.description,
        msdsUrl: chemical.msdsUrl,
        stopped: chemical.stopped,
        storageType,
      });
      setMessage({ type: 'success', text: `${chemical.chemicalCode} ${t('chemicalTypeUpdated')}` });
    } catch (error) {
      setChemicals((previousChemicals) => previousChemicals.map((item) => (
        item.id === chemical.id ? { ...item, storageType: previousStorageType } : item
      )));
      setMessage({ type: 'error', text: error.response?.data?.message || t('chemicalTypeUpdateFailed') });
    }
  };

  return <Box>
    <Typography variant="h4" sx={{ mb: 3, fontWeight: 600 }}>{t('chemicalsTitle')}</Typography>
    {message && <Alert severity={message.type} sx={{ mb: 2 }}>{String(message.text)}</Alert>}
    {canManage && <Paper component="form" onSubmit={create} sx={{ p: 3, mb: 3, display: 'grid', gridTemplateColumns: { xs: '1fr', md: '1fr 2fr 1.5fr 2fr 2fr auto' }, gap: 2 }}>
      <TextField required label={t('chemicalCode')} value={form.chemicalCode} onChange={(e) => setForm({ ...form, chemicalCode: e.target.value })} />
      <TextField required label={t('chemicalName')} value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
      <TextField required select label={t('storageType')} value={form.storageType} onChange={(e) => setForm({ ...form, storageType: e.target.value })}>{storageTypes.map((item) => <MenuItem key={item.value} value={item.value}>{item.label}</MenuItem>)}</TextField>
      <TextField label={t('description')} value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
      <TextField label={t('msdsUrl')} value={form.msdsUrl} onChange={(e) => setForm({ ...form, msdsUrl: e.target.value })} />
      <Button type="submit" variant="contained">{t('saveCreate')}</Button>
    </Paper>}
    <Paper><Table><TableHead><TableRow><TableCell>{t('code')}</TableCell><TableCell>{t('name')}</TableCell><TableCell>{t('storageType')}</TableCell><TableCell>{t('description')}</TableCell><TableCell>{t('status')}</TableCell><TableCell /></TableRow></TableHead>
      <TableBody>{chemicals.map((chemical) => <TableRow key={chemical.id}><TableCell>{chemical.chemicalCode}</TableCell><TableCell>{chemical.name}</TableCell><TableCell><TextField select size="small" disabled={!canManage} value={chemical.storageType || 'GENERAL'} onChange={(e) => updateStorageType(chemical, e.target.value)} sx={{ minWidth: 130 }}>{storageTypes.map((item) => <MenuItem key={item.value} value={item.value}>{item.label}</MenuItem>)}</TextField></TableCell><TableCell>{chemical.description || '-'}</TableCell><TableCell>{chemical.stopped ? t('stopped') : t('active')}</TableCell><TableCell align="right">{canManage && <Button color="error" onClick={() => remove(chemical)}>{t('delete')}</Button>}</TableCell></TableRow>)}</TableBody>
    </Table></Paper>
  </Box>;
}
