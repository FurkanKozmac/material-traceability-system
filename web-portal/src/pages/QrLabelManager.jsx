import React, { useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Autocomplete,
  Box,
  Button,
  Checkbox,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  FormControlLabel,
  IconButton,
  Paper,
  Skeleton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from '@mui/material';
import { Close, LocalPrintshop, QrCode2, SellOutlined, TaskAlt, Visibility } from '@mui/icons-material';
import QRCode from 'qrcode';
import api from '../api';
import { useLanguage } from '../useLanguage';

const isExpired = (unit) => Boolean(unit.expirationDate && new Date(unit.expirationDate) < new Date());
const isAvailable = (unit) => ['InStock', 'AVAILABLE'].includes(unit.status) && !isExpired(unit);

const getStatus = (unit, t) => {
  if (isExpired(unit)) return { label: t('expired'), color: '#b91c1c', background: '#fee2e2' };
  if (isAvailable(unit)) return { label: t('available'), color: '#15803d', background: '#dcfce7' };
  if (unit.status === 'Depleted') return { label: t('depleted'), color: '#4b5563', background: '#f3f4f6' };
  return { label: unit.status, color: '#92400e', background: '#fef3c7' };
};

const formatDate = (value) => value ? new Date(value).toLocaleDateString() : '-';

const createQr = (value, width = 360) => QRCode.toDataURL(value, {
  width,
  margin: 1,
  errorCorrectionLevel: 'M',
});

export default function QrLabelManager() {
  const { t } = useLanguage();
  const [batches, setBatches] = useState([]);
  const [selectedBatch, setSelectedBatch] = useState(null);
  const [labels, setLabels] = useState([]);
  const [selectedIds, setSelectedIds] = useState(new Set());
  const [loadingBatches, setLoadingBatches] = useState(true);
  const [loadingLabels, setLoadingLabels] = useState(false);
  const [message, setMessage] = useState(null);
  const [batchQrDataUrl, setBatchQrDataUrl] = useState('');
  const [previewUnit, setPreviewUnit] = useState(null);
  const [previewQrDataUrl, setPreviewQrDataUrl] = useState('');
  const [printLabels, setPrintLabels] = useState([]);
  const [printMode, setPrintMode] = useState('units');

  useEffect(() => {
    api.get('/batches')
      .then((response) => setBatches(response.data.items))
      .catch(() => setMessage({ type: 'error', text: t('labelsLoadFailed') }))
      .finally(() => setLoadingBatches(false));
  }, [t]);

  useEffect(() => {
    if (!selectedBatch) {
      setLabels([]);
      setSelectedIds(new Set());
      setBatchQrDataUrl('');
      return undefined;
    }

    let active = true;
    setLoadingLabels(true);
    setMessage(null);
    setLabels([]);
    setSelectedIds(new Set());

    api.get(`/batches/${selectedBatch.id}/units`)
      .then((response) => active && setLabels(response.data))
      .catch(() => active && setMessage({ type: 'error', text: t('batchLabelsLoadFailed') }))
      .finally(() => active && setLoadingLabels(false));

    createQr(`BAT-${selectedBatch.batchNo}`, 240)
      .then((dataUrl) => active && setBatchQrDataUrl(dataUrl))
      .catch(() => active && setBatchQrDataUrl(''));

    return () => { active = false; };
  }, [selectedBatch, t]);

  useEffect(() => {
    if (!previewUnit) {
      setPreviewQrDataUrl('');
      return undefined;
    }
    let active = true;
    createQr(previewUnit.barcode, 420)
      .then((dataUrl) => active && setPreviewQrDataUrl(dataUrl))
      .catch(() => active && setPreviewQrDataUrl(''));
    return () => { active = false; };
  }, [previewUnit]);

  const allSelected = labels.length > 0 && selectedIds.size === labels.length;
  const partiallySelected = selectedIds.size > 0 && !allSelected;
  const selectedCount = selectedIds.size;

  const batchSummary = useMemo(() => selectedBatch ? [
    [t('batchSummaryLabel'), selectedBatch.batchNo],
    [t('chemicalSummaryLabel'), selectedBatch.chemicalName],
    [t('total'), `${labels.length || selectedBatch.unitCount || 0} ${t('labelsCount')}`],
    [t('expDate'), formatDate(selectedBatch.expirationDate)],
  ] : [], [selectedBatch, labels.length, t]);

  const toggleLabel = (id) => {
    setSelectedIds((current) => {
      const next = new Set(current);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const toggleAll = () => setSelectedIds(allSelected ? new Set() : new Set(labels.map((unit) => unit.id)));
  const selectAvailable = () => setSelectedIds(new Set(labels.filter(isAvailable).map((unit) => unit.id)));

  const print = async (mode) => {
    setPrintMode(mode);
    if (mode === 'batch') {
      setPrintLabels([]);
    } else {
      const selected = labels.filter((unit) => selectedIds.has(unit.id));
      const printable = await Promise.all(selected.map(async (unit) => ({
        ...unit,
        qrDataUrl: await createQr(unit.barcode, 420),
      })));
      setPrintLabels(printable);
    }
    requestAnimationFrame(() => requestAnimationFrame(() => window.print()));
  };

  const statusFor = (unit) => getStatus(unit, t);

  return <Box className={`qr-page print-mode-${printMode}`} sx={{ width: '100%', maxWidth: 1560, mx: 'auto', pb: 5 }}>
    <Box className="no-print" sx={{ mb: 3 }}>
      <Typography variant="h4" sx={{ fontWeight: 750, color: '#111827', mb: 0.75 }}>{t('qrLabelTitle')}</Typography>
      <Typography variant="body2" sx={{ color: '#64748b' }}>{t('qrLabelSubtitle')}</Typography>
    </Box>

    <Paper className="no-print" variant="outlined" sx={{ p: { xs: 2, md: 2.5 }, mb: 2.5, borderRadius: 3, borderColor: '#e2e8f0' }}>
      <Autocomplete
        fullWidth
        loading={loadingBatches}
        options={batches}
        value={selectedBatch}
        onChange={(_, value) => setSelectedBatch(value)}
        getOptionLabel={(batch) => `${batch.batchNo} · ${batch.chemicalName}`}
        isOptionEqualToValue={(option, value) => option.id === value.id}
        noOptionsText="-"
        loadingText="..."
        slotProps={{ listbox: { sx: { maxHeight: 336 } } }}
        renderOption={(props, batch) => <Box component="li" {...props} key={batch.id} sx={{ py: '10px !important', gap: 1.5 }}>
          <Box sx={{ minWidth: 0, flex: 1 }}><Typography sx={{ fontWeight: 650, color: '#1e293b' }}>{batch.batchNo}</Typography><Typography variant="caption" sx={{ color: '#64748b' }}>{batch.chemicalName}</Typography></Box>
          <Chip size="small" label={`${batch.unitCount} ${t('labelsCount')}`} sx={{ backgroundColor: '#f1f5f9', color: '#475569' }} />
        </Box>}
        renderInput={(params) => <TextField {...params} label={t('rawMaterialBatch')} placeholder={t('selectBatchPlaceholder')} />}
      />

      {selectedBatch && <Box sx={{ mt: 2.5, display: 'grid', gridTemplateColumns: { xs: '1fr 1fr', md: 'repeat(4, 1fr)' }, border: '1px solid #e2e8f0', borderRadius: 2, overflow: 'hidden', backgroundColor: '#f8fafc' }}>
        {batchSummary.map(([label, value], index) => <Box key={label} sx={{ px: 2.5, py: 1.75, borderRight: { md: index < 3 ? '1px solid #e2e8f0' : 'none' }, borderBottom: { xs: index < 2 ? '1px solid #e2e8f0' : 'none', md: 'none' } }}><Typography variant="caption" sx={{ color: '#64748b', fontWeight: 600 }}>{label}</Typography><Typography sx={{ color: '#0f172a', fontWeight: 700, mt: 0.25 }}>{value}</Typography></Box>)}
      </Box>}
    </Paper>

    {message && <Alert className="no-print" severity={message.type} sx={{ mb: 2 }}>{message.text}</Alert>}

    {!selectedBatch && !loadingBatches && <Paper className="no-print" variant="outlined" sx={{ minHeight: 320, display: 'grid', placeItems: 'center', borderRadius: 3, borderStyle: 'dashed', borderColor: '#cbd5e1', backgroundColor: '#fbfdff' }}><Stack alignItems="center" spacing={1.5} sx={{ px: 3, textAlign: 'center' }}><Box sx={{ width: 64, height: 64, borderRadius: '50%', display: 'grid', placeItems: 'center', backgroundColor: '#eff6ff', color: '#2563eb' }}><SellOutlined fontSize="large" /></Box><Typography sx={{ fontWeight: 700, color: '#334155' }}>{t('noBatchSelected')}</Typography><Typography variant="body2" sx={{ color: '#64748b', maxWidth: 480 }}>{t('noBatchSelectedDesc')}</Typography></Stack></Paper>}

    {selectedBatch && <>
      <Paper className="batch-label-panel no-print" variant="outlined" sx={{ mb: 2.5, p: 2, borderRadius: 3, borderColor: '#bfdbfe', backgroundColor: '#f8fbff', display: 'flex', alignItems: 'center', gap: 2.5, flexWrap: 'wrap' }}>
        <Box className="batch-print-label" sx={{ width: 360, maxWidth: '100%', p: 2, border: '1.5px solid #0f172a', borderRadius: 2, backgroundColor: '#fff', display: 'grid', gridTemplateColumns: '120px 1fr', gap: 2, alignItems: 'center' }}>
          {batchQrDataUrl && <img src={batchQrDataUrl} alt={`QR BAT-${selectedBatch.batchNo}`} width="120" height="120" />}
          <Box sx={{ minWidth: 0 }}><Typography variant="caption" sx={{ fontWeight: 800, letterSpacing: '0.08em', color: '#475569' }}>{t('rawMaterialBatch').toUpperCase()}</Typography><Typography sx={{ fontFamily: 'monospace', fontWeight: 900, fontSize: '1.1rem', overflowWrap: 'anywhere' }}>BAT-{selectedBatch.batchNo}</Typography><Typography variant="body2" sx={{ mt: 0.5, fontWeight: 700 }}>{selectedBatch.chemicalName}</Typography><Typography variant="caption" display="block">{t('total')}: {labels.length || selectedBatch.unitCount || 0}</Typography><Typography variant="caption" display="block">{t('expDate')}: {formatDate(selectedBatch.expirationDate)}</Typography></Box>
        </Box>
        <Box sx={{ flex: 1, minWidth: 220 }}><Typography sx={{ fontWeight: 750, color: '#0f172a' }}>{t('batchOpeningLabel')}</Typography><Typography variant="body2" sx={{ color: '#64748b', mt: 0.5, mb: 1.5 }}>{t('batchOpeningLabelDesc')}</Typography><Button variant="outlined" startIcon={<QrCode2 />} onClick={() => print('batch')} disabled={!batchQrDataUrl} sx={{ textTransform: 'none', fontWeight: 700 }}>{t('printBatchLabel')}</Button></Box>
      </Paper>

      <Paper className="no-print action-bar" elevation={1} sx={{ position: 'sticky', top: 76, zIndex: 10, mb: 2.5, px: 2, py: 1.25, borderRadius: 2.5, border: '1px solid #e2e8f0', display: 'flex', alignItems: 'center', gap: 1.5, flexWrap: 'wrap' }}>
        <FormControlLabel sx={{ mr: 0.5 }} control={<Checkbox checked={allSelected} indeterminate={partiallySelected} onChange={toggleAll} disabled={!labels.length || loadingLabels} />} label={<Typography variant="body2" sx={{ fontWeight: 600 }}>{t('selectAll')}</Typography>} />
        <Divider orientation="vertical" flexItem sx={{ display: { xs: 'none', sm: 'block' } }} />
        <Button variant="text" startIcon={<TaskAlt />} onClick={selectAvailable} disabled={!labels.length || loadingLabels} sx={{ textTransform: 'none', fontWeight: 650 }}>{t('selectAvailableOnly')}</Button>
        <Typography variant="body2" sx={{ color: '#475569', fontWeight: 600, ml: { md: 'auto' } }}>{t('selectedCount')}: <Box component="span" sx={{ color: '#1d4ed8' }}>{selectedCount}</Box> / {labels.length} {t('labelsCount')}</Typography>
        <Button variant="contained" startIcon={<LocalPrintshop />} onClick={() => print('units')} disabled={!selectedCount} sx={{ textTransform: 'none', fontWeight: 700, px: 2.5 }}>{selectedCount} {t('printSelected')}</Button>
      </Paper>

      {loadingLabels ? <Box className="no-print" sx={{ display: 'grid', gap: 1.5 }}>{Array.from({ length: 6 }, (_, index) => <Skeleton key={index} variant="rounded" height={62} />)}</Box> : <TableContainer component={Paper} className="no-print" variant="outlined" sx={{ borderRadius: 3, borderColor: '#e2e8f0' }}>
        <Table size="small">
          <TableHead><TableRow sx={{ backgroundColor: '#f8fafc' }}><TableCell padding="checkbox"><Typography variant="caption" fontWeight={800}>#</Typography></TableCell><TableCell><Typography variant="caption" fontWeight={800}>{t('barcode')}</Typography></TableCell><TableCell><Typography variant="caption" fontWeight={800}>{t('status')}</Typography></TableCell><TableCell><Typography variant="caption" fontWeight={800}>{t('expDate')}</Typography></TableCell><TableCell align="right"><Typography variant="caption" fontWeight={800}>{t('actions')}</Typography></TableCell></TableRow></TableHead>
          <TableBody>{labels.length === 0 ? <TableRow><TableCell colSpan={5} align="center" sx={{ py: 6 }}>{t('noLabels')}</TableCell></TableRow> : labels.map((unit) => { const status = statusFor(unit); const selected = selectedIds.has(unit.id); return <TableRow key={unit.id} hover selected={selected} onClick={() => setPreviewUnit(unit)} sx={{ cursor: 'pointer', '& td': { borderBottomColor: '#eef2f7' } }}>
            <TableCell padding="checkbox" onClick={(event) => event.stopPropagation()}><Checkbox checked={selected} onChange={() => toggleLabel(unit.id)} inputProps={{ 'aria-label': `${unit.barcode} ${t('selectLabel')}` }} /></TableCell>
            <TableCell><Typography sx={{ fontFamily: 'monospace', fontWeight: 800, color: '#0f172a' }}>{unit.barcode}</Typography><Typography variant="caption" color="text.secondary">{selectedBatch.chemicalName} · {selectedBatch.batchNo}</Typography></TableCell>
            <TableCell><Chip size="small" label={status.label} sx={{ color: status.color, backgroundColor: status.background, fontWeight: 700 }} /></TableCell>
            <TableCell sx={{ color: '#475569', whiteSpace: 'nowrap' }}>{formatDate(unit.expirationDate)}</TableCell>
            <TableCell align="right"><Button size="small" variant="outlined" startIcon={<Visibility />} onClick={(event) => { event.stopPropagation(); setPreviewUnit(unit); }} sx={{ textTransform: 'none', fontWeight: 700 }}>{t('previewQr')}</Button></TableCell>
          </TableRow>; })}</TableBody>
        </Table>
      </TableContainer>}
    </>}

    <Box className="print-sheet">{printLabels.map((unit) => <Box key={unit.id} className="print-label"><img src={unit.qrDataUrl} alt={`QR ${unit.barcode}`} /><Typography sx={{ fontFamily: 'monospace', fontWeight: 800 }}>{unit.barcode}</Typography><Typography>{selectedBatch?.chemicalName}</Typography><Typography>{selectedBatch?.batchNo}</Typography><Typography>{t('expDate')}: {formatDate(unit.expirationDate)}</Typography></Box>)}</Box>

    <Dialog open={Boolean(previewUnit)} onClose={() => setPreviewUnit(null)} maxWidth="sm" fullWidth PaperProps={{ sx: { borderRadius: 3 } }}>
      <DialogTitle sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', pb: 1 }}>{t('qrPreview')}<IconButton aria-label={t('close')} onClick={() => setPreviewUnit(null)}><Close /></IconButton></DialogTitle>
      <DialogContent dividers>
        {previewUnit && <Stack spacing={2.5} alignItems="center">
          {previewQrDataUrl ? <Box component="img" src={previewQrDataUrl} alt={`QR ${previewUnit.barcode}`} sx={{ width: 300, height: 300, maxWidth: '100%', imageRendering: 'pixelated' }} /> : <Skeleton variant="rounded" width={300} height={300} />}
          <Typography sx={{ fontFamily: 'monospace', fontWeight: 900, fontSize: '1.2rem', color: '#0f172a' }}>{previewUnit.barcode}</Typography>
          <Box sx={{ width: '100%', display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 1.5, p: 2, borderRadius: 2, backgroundColor: '#f8fafc' }}>
            <Box><Typography variant="caption" color="text.secondary">{t('chemicalSummaryLabel')}</Typography><Typography fontWeight={700}>{selectedBatch?.chemicalName || '-'}</Typography></Box>
            <Box><Typography variant="caption" color="text.secondary">{t('batchSummaryLabel')}</Typography><Typography fontWeight={700}>{selectedBatch?.batchNo || '-'}</Typography></Box>
            <Box><Typography variant="caption" color="text.secondary">{t('rack')}</Typography><Typography fontWeight={700}>{previewUnit.addressCode || t('notAssigned')}</Typography></Box>
            <Box><Typography variant="caption" color="text.secondary">{t('expDate')}</Typography><Typography fontWeight={700}>{formatDate(previewUnit.expirationDate)}</Typography></Box>
          </Box>
        </Stack>}
      </DialogContent>
      <DialogActions sx={{ p: 2 }}><Button onClick={() => setPreviewUnit(null)}>{t('close')}</Button><Button variant="contained" startIcon={<LocalPrintshop />} disabled={!previewQrDataUrl} onClick={() => { setPrintLabels([{ ...previewUnit, qrDataUrl: previewQrDataUrl }]); setPrintMode('units'); setPreviewUnit(null); requestAnimationFrame(() => requestAnimationFrame(() => window.print())); }}>{t('printThisLabel')}</Button></DialogActions>
    </Dialog>

    <style>{`
      .print-sheet { display: none; }
      @page { size: A4 portrait; margin: 9mm; }
      @media print {
        body * { visibility: hidden !important; }
        .print-sheet, .print-sheet * { visibility: visible !important; }
        .print-sheet { display: grid !important; grid-template-columns: repeat(3, 1fr); gap: 4mm; position: absolute; inset: 0; width: 192mm; margin: 0 auto; }
        .print-mode-batch .print-sheet { display: none !important; }
        .print-mode-batch .batch-print-label, .print-mode-batch .batch-print-label * { visibility: visible !important; }
        .print-mode-batch .batch-print-label { display: grid !important; position: absolute; inset: 0 auto auto 0; width: 92mm; min-height: 48mm; padding: 4mm; grid-template-columns: 38mm 1fr; gap: 4mm; border: 1.5px solid #000; background: #fff; }
        .print-mode-batch .batch-print-label img { width: 38mm !important; height: 38mm !important; }
        .print-label { min-height: 78mm; padding: 4mm; border: 1.5px solid #000; border-radius: 2mm; text-align: center; break-inside: avoid; page-break-inside: avoid; color: #000; background: #fff; }
        .print-label img { width: 38mm; height: 38mm; image-rendering: pixelated; }
        .print-label p { margin: 1mm 0; }
      }
    `}</style>
  </Box>;
}
