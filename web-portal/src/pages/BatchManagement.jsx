import React, { useEffect, useState } from 'react';
import { Alert, Box, Button, MenuItem, Paper, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography } from '@mui/material';
import api from '../api';
import { isAdmin } from '../auth';
import { useLanguage } from '../useLanguage';

const emptyForm = { batchNo: '', supplier: '', initialQuantity: 1, expirationDate: '', chemicalId: '' };

export default function BatchManagement() {
  const { t } = useLanguage();
  const canManage = isAdmin();
  const [batches, setBatches] = useState([]);
  const [chemicals, setChemicals] = useState([]);
  const [form, setForm] = useState(emptyForm);
  const [search, setSearch] = useState('');
  const [message, setMessage] = useState(null);

  const load = async (term = search) => {
    const [batchResponse, chemicalResponse] = await Promise.all([
      api.get('/batches', { params: term.trim() ? { search: term.trim() } : {} }),
      api.get('/chemicals'),
    ]);
    setBatches(batchResponse.data.items);
    setChemicals(chemicalResponse.data);
  };
  useEffect(() => { load().catch(() => setMessage({ type: 'error', text: t('batchLoadFailed') })); }, [t]);

  const create = async (event) => {
    event.preventDefault();
    try {
      await api.post('/batches', { ...form, supplier: form.supplier || null, initialQuantity: Number(form.initialQuantity), chemicalId: Number(form.chemicalId), expirationDate: form.expirationDate ? new Date(form.expirationDate).toISOString() : null });
      setForm(emptyForm);
      setMessage({ type: 'success', text: t('batchCreated') });
      await load();
    } catch (error) {
      setMessage({ type: 'error', text: error.response?.data?.message || error.response?.data || t('batchCreateFailed') });
    }
  };

  return <Box>
    <Typography variant="h4" sx={{ mb: 3, fontWeight: 600 }}>{t('batchesTitle')}</Typography>
    {message && <Alert severity={message.type} sx={{ mb: 2 }}>{String(message.text)}</Alert>}
    <Paper component="form" onSubmit={(event) => { event.preventDefault(); load(); }} sx={{ p: 2, mb: 2, display: 'flex', gap: 1 }}>
      <TextField fullWidth label={t('searchBatch')} placeholder={t('searchBatchPlaceholder')} value={search} onChange={(e) => setSearch(e.target.value)} />
      <Button type="submit" variant="outlined">{t('search')}</Button>
      <Button onClick={() => { setSearch(''); load(''); }}>{t('clear')}</Button>
    </Paper>
    {canManage && <Paper component="form" onSubmit={create} sx={{ p: 3, mb: 3, display: 'grid', gridTemplateColumns: { xs: '1fr', md: 'repeat(5, 1fr) auto' }, gap: 2 }}>
      <TextField required label={t('batchNo')} value={form.batchNo} onChange={(e) => setForm({ ...form, batchNo: e.target.value })} />
      <TextField label={t('supplier')} value={form.supplier} onChange={(e) => setForm({ ...form, supplier: e.target.value })} />
      <TextField required type="number" label={t('initialQuantity')} slotProps={{ htmlInput: { min: 1, max: 10000 } }} value={form.initialQuantity} onChange={(e) => setForm({ ...form, initialQuantity: e.target.value })} />
      <TextField type="date" label={t('expirationDate')} slotProps={{ inputLabel: { shrink: true } }} value={form.expirationDate} onChange={(e) => setForm({ ...form, expirationDate: e.target.value })} />
      <TextField required select label={t('selectChemical')} value={form.chemicalId} onChange={(e) => setForm({ ...form, chemicalId: e.target.value })}>{chemicals.map((chemical) => <MenuItem key={chemical.id} value={chemical.id}>{chemical.chemicalCode} · {chemical.name}</MenuItem>)}</TextField>
      <Button type="submit" variant="contained">{t('saveCreate')}</Button>
    </Paper>}
    <Paper><Table><TableHead><TableRow><TableCell>{t('batchNo')}</TableCell><TableCell>{t('chemical')}</TableCell><TableCell>{t('supplier')}</TableCell><TableCell>{t('initialQuantity')}</TableCell><TableCell>{t('stockUnit')}</TableCell><TableCell>{t('expirationDate')}</TableCell></TableRow></TableHead>
      <TableBody>{batches.map((batch) => <TableRow key={batch.id}><TableCell>{batch.batchNo}</TableCell><TableCell>{batch.chemicalName}</TableCell><TableCell>{batch.supplier || '-'}</TableCell><TableCell>{batch.initialQuantity}</TableCell><TableCell>{batch.unitCount}</TableCell><TableCell>{batch.expirationDate ? new Date(batch.expirationDate).toLocaleDateString() : '-'}</TableCell></TableRow>)}</TableBody>
    </Table></Paper>
  </Box>;
}
