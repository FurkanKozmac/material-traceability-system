import React, { useEffect, useRef, useState } from 'react';
import { Alert, Box, Button, CircularProgress, Dialog, DialogActions, DialogContent, DialogTitle, IconButton, MenuItem, Paper, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography } from '@mui/material';
import { Close, PhotoCamera } from '@mui/icons-material';
import api from '../api';
import { isAdmin, isOperator } from '../auth';
import { useLanguage } from '../useLanguage';
import { getLocalizedApiError } from '../apiError';

const emptyForm = { batchNo: '', supplier: '', initialQuantity: 1, expirationDate: '', chemicalId: '' };

export default function BatchManagement() {
  const { t } = useLanguage();
  const canCreateBatch = isAdmin() || isOperator();
  const [batches, setBatches] = useState([]);
  const [chemicals, setChemicals] = useState([]);
  const [form, setForm] = useState(emptyForm);
  const [search, setSearch] = useState('');
  const [message, setMessage] = useState(null);
  const [scanning, setScanning] = useState(false);
  const [labelPreview, setLabelPreview] = useState(null);
  const imageInput = useRef(null);

  useEffect(() => () => {
    if (labelPreview?.imageUrl) URL.revokeObjectURL(labelPreview.imageUrl);
  }, [labelPreview]);

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
    let createdBatch;
    try {
      const response = await api.post('/batches', { ...form, supplier: form.supplier || null, initialQuantity: Number(form.initialQuantity), chemicalId: Number(form.chemicalId), expirationDate: form.expirationDate ? new Date(form.expirationDate).toISOString() : null });
      createdBatch = response.data;
      setForm(emptyForm);
      setMessage({ type: 'success', text: t('batchCreated') });
    } catch (error) {
      setMessage({ type: 'error', text: getLocalizedApiError(error, t, t('batchCreateFailed')) });
      return;
    }

    await load().catch(() => setMessage({ type: 'warning', text: t('batchCreatedPreviewFailed') }));
    try {
      const imageResponse = await api.get('/labels/preview', {
        params: { payload: `BAT-${createdBatch.batchNo}` },
        responseType: 'blob',
      });
      setLabelPreview({
        batch: createdBatch,
        imageUrl: URL.createObjectURL(imageResponse.data),
        payload: `BAT-${createdBatch.batchNo}`,
      });
    } catch {
      setMessage({ type: 'warning', text: t('batchCreatedPreviewFailed') });
    }
  };

  const scanLabel = async (event) => {
    const image = event.target.files?.[0];
    event.target.value = '';
    if (!image) return;

    const body = new FormData();
    body.append('image', image);
    setScanning(true);
    setMessage(null);
    try {
      const response = await api.post('/batches/scan-label', body);
      const result = response.data;
      setForm((current) => ({
        ...current,
        batchNo: result.extractedLotNumber || current.batchNo,
        expirationDate: result.extractedExpirationDate?.slice(0, 10) || current.expirationDate,
        chemicalId: result.suggestedChemicalId ? String(result.suggestedChemicalId) : current.chemicalId,
      }));
      setMessage({
        type: 'success',
        text: result.suggestedChemicalName ? t('ocrScanComplete') : t('ocrScanCompleteNoChemical'),
      });
    } catch (error) {
      setMessage({ type: 'error', text: getLocalizedApiError(error, t, t('ocrScanFailed')) });
    } finally {
      setScanning(false);
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
    {canCreateBatch && <Paper component="form" onSubmit={create} sx={{ p: 3, mb: 3, display: 'grid', gridTemplateColumns: { xs: '1fr', md: 'repeat(5, 1fr) auto' }, gap: 2 }}>
      <Box sx={{ gridColumn: '1 / -1', display: 'flex', alignItems: 'center', gap: 1.5, flexWrap: 'wrap' }}>
        <input ref={imageInput} hidden type="file" accept="image/jpeg,image/png,image/webp" onChange={scanLabel} />
        <Button type="button" variant="outlined" startIcon={scanning ? <CircularProgress size={18} /> : <PhotoCamera />} disabled={scanning} onClick={() => imageInput.current?.click()}>
          {scanning ? t('ocrScanning') : t('ocrScanLabel')}
        </Button>
        <Typography variant="caption" color="text.secondary">{t('ocrScanHint')}</Typography>
      </Box>
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
    <Dialog open={Boolean(labelPreview)} onClose={() => setLabelPreview(null)} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        {t('batchLabelPreview')}
        <IconButton aria-label={t('close')} onClick={() => setLabelPreview(null)}><Close /></IconButton>
      </DialogTitle>
      <DialogContent dividers>
        {labelPreview && <Box sx={{ display: 'grid', justifyItems: 'center', gap: 2 }}>
          <Box component="img" src={labelPreview.imageUrl} alt={`${t('batchLabelPreview')} ${labelPreview.payload}`} sx={{ width: 280, maxWidth: '100%', imageRendering: 'pixelated' }} />
          <Typography sx={{ fontFamily: 'monospace', fontWeight: 800 }}>{labelPreview.payload}</Typography>
          <Typography>{labelPreview.batch.chemicalName} · {labelPreview.batch.batchNo}</Typography>
        </Box>}
      </DialogContent>
      <DialogActions><Button onClick={() => setLabelPreview(null)}>{t('close')}</Button></DialogActions>
    </Dialog>
  </Box>;
}
