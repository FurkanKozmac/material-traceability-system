import React, { useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Autocomplete,
  Box,
  Button,
  Checkbox,
  Chip,
  Divider,
  FormControlLabel,
  Paper,
  Skeleton,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { LocalPrintshop, QrCode2, SellOutlined, TaskAlt } from '@mui/icons-material';
import QRCode from 'qrcode';
import api from '../api';
import { useLanguage } from '../LanguageContext';

const isExpired = (unit) => Boolean(unit.expirationDate && new Date(unit.expirationDate) < new Date());
const isAvailable = (unit) => ['InStock', 'AVAILABLE'].includes(unit.status) && !isExpired(unit);

const getStatus = (unit, t) => {
  if (isExpired(unit)) return { label: t('expired'), color: '#b91c1c', background: '#fee2e2' };
  if (isAvailable(unit)) return { label: t('available'), color: '#15803d', background: '#dcfce7' };
  if (unit.status === 'Depleted') return { label: t('depleted'), color: '#4b5563', background: '#f3f4f6' };
  return { label: unit.status, color: '#92400e', background: '#fef3c7' };
};

const formatDate = (value, short = false) => {
  if (!value) return '-';
  return new Date(value).toLocaleDateString('tr-TR', short
    ? { day: '2-digit', month: '2-digit', year: '2-digit' }
    : undefined);
};

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
  const [printMode, setPrintMode] = useState('units');

  useEffect(() => {
    api.get('/batches')
      .then((response) => setBatches(response.data))
      .catch(() => setMessage({ type: 'error', text: 'Hammadde partileri yüklenemedi.' }))
      .finally(() => setLoadingBatches(false));
  }, []);

  useEffect(() => {
    if (!selectedBatch) {
      setLabels([]);
      setSelectedIds(new Set());
      return;
    }

    let active = true;
    setLoadingLabels(true);
    setMessage(null);
    setLabels([]);
    setSelectedIds(new Set());

    api.get(`/batches/${selectedBatch.id}/units`)
      .then(async (response) => {
        const generated = await Promise.all(response.data.map(async (unit) => ({
          ...unit,
          qrDataUrl: await QRCode.toDataURL(unit.barcode, {
            width: 180,
            margin: 1,
            errorCorrectionLevel: 'M',
          }),
        })));
        if (active) setLabels(generated);
      })
      .catch(() => active && setMessage({ type: 'error', text: 'Parti etiketleri yüklenemedi.' }))
      .finally(() => active && setLoadingLabels(false));

    return () => { active = false; };
  }, [selectedBatch]);

  useEffect(() => {
    let active = true;
    if (!selectedBatch) {
      setBatchQrDataUrl('');
      return undefined;
    }

    QRCode.toDataURL(`BAT-${selectedBatch.batchNo}`, {
      width: 240,
      margin: 1,
      errorCorrectionLevel: 'M',
    }).then((dataUrl) => active && setBatchQrDataUrl(dataUrl));

    return () => { active = false; };
  }, [selectedBatch]);

  const allSelected = labels.length > 0 && selectedIds.size === labels.length;
  const partiallySelected = selectedIds.size > 0 && !allSelected;
  const selectedCount = selectedIds.size;

  const batchSummary = useMemo(() => selectedBatch ? [
    ['Parti', selectedBatch.batchNo],
    ['Kimyasal', selectedBatch.chemicalName],
    ['Toplam', `${labels.length || selectedBatch.unitCount || 0} Varil`],
    ['SKT', formatDate(selectedBatch.expirationDate)],
  ] : [], [selectedBatch, labels.length]);

  const toggleLabel = (id) => {
    setSelectedIds((current) => {
      const next = new Set(current);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const toggleAll = () => {
    setSelectedIds(allSelected ? new Set() : new Set(labels.map((unit) => unit.id)));
  };

  const selectAvailable = () => {
    setSelectedIds(new Set(labels.filter(isAvailable).map((unit) => unit.id)));
  };

  const print = (mode) => {
    setPrintMode(mode);
    requestAnimationFrame(() => requestAnimationFrame(() => window.print()));
  };

  return <Box className={`qr-page print-mode-${printMode}`} sx={{ width: '100%', maxWidth: 1560, mx: 'auto', pb: 5 }}>
    <Box className="no-print" sx={{ mb: 3 }}>
      <Typography variant="h4" sx={{ fontWeight: 750, color: '#111827', mb: 0.75 }}>
        {t('qrLabelTitle')}
      </Typography>
      <Typography variant="body2" sx={{ color: '#64748b' }}>
        {t('qrLabelSubtitle')}
      </Typography>
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
        renderOption={(props, batch) => (
          <Box component="li" {...props} key={batch.id} sx={{ py: '10px !important', gap: 1.5 }}>
            <Box sx={{ minWidth: 0, flex: 1 }}>
              <Typography sx={{ fontWeight: 650, color: '#1e293b' }}>{batch.batchNo}</Typography>
              <Typography variant="caption" sx={{ color: '#64748b' }}>{batch.chemicalName}</Typography>
            </Box>
            <Chip size="small" label={`${batch.unitCount} varil`} sx={{ backgroundColor: '#f1f5f9', color: '#475569' }} />
          </Box>
        )}
        renderInput={(params) => (
          <TextField
            {...params}
            label={t('rawMaterialBatch')}
            placeholder={t('selectBatchPlaceholder')}
          />
        )}
      />

      {selectedBatch && <Box sx={{ mt: 2.5, display: 'grid', gridTemplateColumns: { xs: '1fr 1fr', md: 'repeat(4, 1fr)' }, border: '1px solid #e2e8f0', borderRadius: 2, overflow: 'hidden', backgroundColor: '#f8fafc' }}>
        {batchSummary.map(([label, value], index) => <Box key={label} sx={{ px: 2.5, py: 1.75, borderRight: { md: index < 3 ? '1px solid #e2e8f0' : 'none' }, borderBottom: { xs: index < 2 ? '1px solid #e2e8f0' : 'none', md: 'none' } }}>
          <Typography variant="caption" sx={{ color: '#64748b', fontWeight: 600 }}>{label}</Typography>
          <Typography sx={{ color: '#0f172a', fontWeight: 700, mt: 0.25 }}>{value}</Typography>
        </Box>)}
      </Box>}
    </Paper>

    {message && <Alert className="no-print" severity={message.type} sx={{ mb: 2 }}>{message.text}</Alert>}

    {!selectedBatch && !loadingBatches && <Paper className="no-print" variant="outlined" sx={{ minHeight: 320, display: 'grid', placeItems: 'center', borderRadius: 3, borderStyle: 'dashed', borderColor: '#cbd5e1', backgroundColor: '#fbfdff' }}>
      <Stack alignItems="center" spacing={1.5} sx={{ px: 3, textAlign: 'center' }}>
        <Box sx={{ width: 64, height: 64, borderRadius: '50%', display: 'grid', placeItems: 'center', backgroundColor: '#eff6ff', color: '#2563eb' }}><SellOutlined fontSize="large" /></Box>
        <Typography sx={{ fontWeight: 700, color: '#334155' }}>{t('noBatchSelected')}</Typography>
        <Typography variant="body2" sx={{ color: '#64748b', maxWidth: 480 }}>
          {t('noBatchSelectedDesc')}
        </Typography>
      </Stack>
    </Paper>}

    {selectedBatch && <>
      <Paper className="batch-label-panel" variant="outlined" sx={{ mb: 2.5, p: 2, borderRadius: 3, borderColor: '#bfdbfe', backgroundColor: '#f8fbff', display: 'flex', alignItems: 'center', gap: 2.5, flexWrap: 'wrap' }}>
        <Box className="batch-print-label" sx={{ width: 360, maxWidth: '100%', p: 2, border: '1.5px solid #0f172a', borderRadius: 2, backgroundColor: '#fff', display: 'grid', gridTemplateColumns: '120px 1fr', gap: 2, alignItems: 'center' }}>
          {batchQrDataUrl && <img src={batchQrDataUrl} alt={`Parti QR BAT-${selectedBatch.batchNo}`} width="120" height="120" />}
          <Box sx={{ minWidth: 0 }}>
            <Typography variant="caption" sx={{ fontWeight: 800, letterSpacing: '0.08em', color: '#475569' }}>{t('rawMaterialBatch').toUpperCase()}</Typography>
            <Typography sx={{ fontFamily: 'monospace', fontWeight: 900, fontSize: '1.1rem', overflowWrap: 'anywhere' }}>BAT-{selectedBatch.batchNo}</Typography>
            <Typography variant="body2" sx={{ mt: 0.5, fontWeight: 700 }}>{selectedBatch.chemicalName}</Typography>
            <Typography variant="caption" display="block">Total: {labels.length || selectedBatch.unitCount || 0} drums</Typography>
            <Typography variant="caption" display="block">{t('expDate')}: {formatDate(selectedBatch.expirationDate)}</Typography>
          </Box>
        </Box>
        <Box className="no-print" sx={{ flex: 1, minWidth: 220 }}>
          <Typography sx={{ fontWeight: 750, color: '#0f172a' }}>{t('batchOpeningLabel')}</Typography>
          <Typography variant="body2" sx={{ color: '#64748b', mt: 0.5, mb: 1.5 }}>
            {t('batchOpeningLabelDesc')}
          </Typography>
          <Button variant="outlined" startIcon={<QrCode2 />} onClick={() => print('batch')} disabled={!batchQrDataUrl} sx={{ textTransform: 'none', fontWeight: 700 }}>
            {t('printBatchLabel')}
          </Button>
        </Box>
      </Paper>

      <Paper className="no-print action-bar" elevation={1} sx={{ position: 'sticky', top: 76, zIndex: 10, mb: 2.5, px: 2, py: 1.25, borderRadius: 2.5, border: '1px solid #e2e8f0', display: 'flex', alignItems: 'center', gap: 1.5, flexWrap: 'wrap' }}>
        <FormControlLabel
          sx={{ mr: 0.5 }}
          control={<Checkbox checked={allSelected} indeterminate={partiallySelected} onChange={toggleAll} disabled={!labels.length || loadingLabels} />}
          label={<Typography variant="body2" sx={{ fontWeight: 600 }}>{t('selectAll')}</Typography>}
        />
        <Divider orientation="vertical" flexItem sx={{ display: { xs: 'none', sm: 'block' } }} />
        <Button variant="text" startIcon={<TaskAlt />} onClick={selectAvailable} disabled={!labels.length || loadingLabels} sx={{ textTransform: 'none', fontWeight: 650 }}>
          {t('selectAvailableOnly')}
        </Button>
        <Typography variant="body2" sx={{ color: '#475569', fontWeight: 600, ml: { md: 'auto' } }}>
          {t('selectedCount')}: <Box component="span" sx={{ color: '#1d4ed8' }}>{selectedCount}</Box> / {labels.length} {t('labelsCount')}
        </Typography>
        <Button variant="contained" startIcon={<LocalPrintshop />} onClick={() => print('units')} disabled={!selectedCount} sx={{ textTransform: 'none', fontWeight: 700, px: 2.5 }}>
          {selectedCount} {t('printSelected')}
        </Button>
      </Paper>

      {loadingLabels ? <Box className="no-print" sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(210px, 1fr))', gap: 2 }}>
        {Array.from({ length: 12 }, (_, index) => <Paper key={index} variant="outlined" sx={{ p: 2, borderRadius: 2.5 }}>
          <Skeleton variant="rounded" height={28} sx={{ mb: 1.5 }} />
          <Skeleton variant="rounded" width={120} height={120} sx={{ mx: 'auto', mb: 1.5 }} />
          <Skeleton width="75%" sx={{ mx: 'auto' }} />
          <Skeleton width="55%" sx={{ mx: 'auto' }} />
          <Skeleton variant="rounded" height={26} sx={{ mt: 1.5 }} />
        </Paper>)}
      </Box> : labels.length === 0 ? <Paper className="no-print" variant="outlined" sx={{ py: 8, textAlign: 'center', borderRadius: 3, borderStyle: 'dashed' }}>
        <Typography sx={{ fontWeight: 700, color: '#475569' }}>-</Typography>
      </Paper> : <Box className="qr-label-grid" sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(210px, 1fr))', gap: 2 }}>
        {labels.map((unit) => {
          const selected = selectedIds.has(unit.id);
          const status = getStatus(unit, t);
          return <Paper
            key={unit.id}
            component="article"
            className={`qr-label ${selected ? 'print-selected' : 'print-hidden'}`}
            variant="outlined"
            onClick={() => toggleLabel(unit.id)}
            sx={{
              width: '100%', maxWidth: 230, minHeight: 292, p: 1.5, mx: 'auto', borderRadius: 2.5,
              cursor: 'pointer', textAlign: 'center', breakInside: 'avoid', transition: 'border-color 150ms, background-color 150ms, box-shadow 150ms',
              borderWidth: selected ? 2 : 1, borderColor: selected ? '#3b82f6' : '#e2e8f0',
              backgroundColor: selected ? 'rgba(239, 246, 255, 0.35)' : '#fff',
              boxShadow: selected ? '0 0 0 3px rgba(59,130,246,0.08)' : '0 1px 2px rgba(15,23,42,0.04)',
              '&:hover': { borderColor: selected ? '#3b82f6' : '#94a3b8', boxShadow: '0 6px 18px rgba(15,23,42,0.08)' },
            }}
          >
            <Box className="card-checkbox" sx={{ display: 'flex', justifyContent: 'flex-end', height: 28 }}>
              <Checkbox checked={selected} onChange={() => toggleLabel(unit.id)} onClick={(event) => event.stopPropagation()} size="small" inputProps={{ 'aria-label': `${unit.barcode} etiketini seç` }} />
            </Box>
            <img src={unit.qrDataUrl} alt={`QR ${unit.barcode}`} width="120" height="120" />
            <Typography sx={{ mt: 0.75, fontFamily: 'monospace', fontSize: '0.93rem', letterSpacing: '0.035em', fontWeight: 800, color: '#0f172a', overflowWrap: 'anywhere' }}>{unit.barcode}</Typography>
            <Typography variant="body2" noWrap title={unit.name} sx={{ mt: 0.5, color: '#64748b' }}>{unit.name}</Typography>
            <Box sx={{ mt: 1.25, pt: 1.25, borderTop: '1px solid #eef2f7', display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 1 }}>
              <Chip size="small" label={status.label} sx={{ height: 24, fontSize: '0.7rem', fontWeight: 700, color: status.color, backgroundColor: status.background }} />
              <Typography variant="caption" sx={{ color: '#475569', whiteSpace: 'nowrap' }}><strong>{t('expDate')}:</strong> {formatDate(unit.expirationDate, true)}</Typography>
            </Box>
          </Paper>;
        })}
      </Box>}
    </>}

    <style>{`
      @page { size: A4 portrait; margin: 9mm; }
      @media print {
        body * { visibility: hidden !important; }
        .no-print, .card-checkbox, .print-hidden { display: none !important; }
        .print-mode-batch .qr-label-grid { display: none !important; }
        .print-mode-batch .batch-label-panel,
        .print-mode-batch .batch-print-label,
        .print-mode-batch .batch-print-label * { visibility: visible !important; }
        .print-mode-batch .batch-label-panel {
          position: absolute !important;
          inset: 0 auto auto 0 !important;
          display: block !important;
          width: 92mm !important;
          padding: 0 !important;
          margin: 0 !important;
          border: 0 !important;
          background: #fff !important;
        }
        .print-mode-batch .batch-print-label {
          display: grid !important;
          width: 92mm !important;
          min-height: 48mm !important;
          padding: 4mm !important;
          grid-template-columns: 38mm 1fr !important;
          gap: 4mm !important;
          border: 1.5px solid #000 !important;
          border-radius: 2mm !important;
          background: #fff !important;
          box-shadow: none !important;
          break-inside: avoid !important;
        }
        .print-mode-batch .batch-print-label img { width: 38mm !important; height: 38mm !important; }
        .print-mode-batch .batch-print-label * { color: #000 !important; background: #fff !important; }
        .print-mode-units .batch-label-panel { display: none !important; }
        .print-mode-units .qr-label-grid,
        .print-mode-units .qr-label-grid * { visibility: visible !important; }
        .qr-label-grid {
          position: absolute !important;
          inset: 0 !important;
          display: grid !important;
          grid-template-columns: repeat(3, 1fr) !important;
          align-items: start !important;
          gap: 4mm !important;
          width: 192mm !important;
          margin: 0 auto !important;
        }
        .qr-label.print-selected {
          display: block !important;
          visibility: visible !important;
          width: auto !important;
          max-width: none !important;
          min-height: 78mm !important;
          padding: 4mm !important;
          border: 1.5px solid #000 !important;
          border-radius: 2mm !important;
          background: #fff !important;
          box-shadow: none !important;
          color: #000 !important;
          break-inside: avoid !important;
          page-break-inside: avoid !important;
        }
        .qr-label.print-selected img { width: 38mm !important; height: 38mm !important; }
        .qr-label.print-selected * { color: #000 !important; background: #fff !important; }
      }
    `}</style>
  </Box>;
}
