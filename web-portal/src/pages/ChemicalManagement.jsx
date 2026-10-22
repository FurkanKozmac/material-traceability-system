import React, { useEffect, useRef, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  MenuItem,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from '@mui/material';
import { CloudUpload } from '@mui/icons-material';
import api from '../api';
import { getLocalizedApiError } from '../apiError';
import { isAdmin } from '../auth';
import { downloadBlob } from '../download';
import { useLanguage } from '../useLanguage';

const emptyForm = { chemicalCode: '', name: '', description: '', msdsUrl: '', storageType: 'GENERAL' };
const maxImportBytes = 10 * 1024 * 1024;
const formatFileSize = (bytes) => (
  bytes < 1024 * 1024
    ? `${Math.max(1, Math.ceil(bytes / 1024))} KB`
    : `${(bytes / (1024 * 1024)).toFixed(1)} MB`
);

export default function ChemicalManagement() {
  const { t } = useLanguage();
  const canManage = isAdmin();
  const [chemicals, setChemicals] = useState([]);
  const [form, setForm] = useState(emptyForm);
  const [message, setMessage] = useState(null);
  const [templateDownloading, setTemplateDownloading] = useState(false);
  const [importOpen, setImportOpen] = useState(false);
  const [importFile, setImportFile] = useState(null);
  const [importDragActive, setImportDragActive] = useState(false);
  const [importing, setImporting] = useState(false);
  const [importResult, setImportResult] = useState(null);
  const [importError, setImportError] = useState(null);
  const importFileInput = useRef(null);
  const storageTypes = [
    { value: 'GENERAL', label: t('general') },
    { value: 'SOLVENT', label: t('solvent') },
    { value: 'FLAMMABLE', label: t('flammable') },
    { value: 'CORROSIVE', label: t('corrosive') },
    { value: 'PAINT', label: t('paint') },
  ];
  const load = async () => setChemicals((await api.get('/chemicals')).data);

  useEffect(() => { load().catch(() => setMessage({ type: 'error', text: t('chemicalLoadFailed') })); }, [t]);

  const resetImportState = () => {
    setImportFile(null);
    setImportDragActive(false);
    setImportResult(null);
    setImportError(null);
  };

  const openImportDialog = () => {
    resetImportState();
    setImportOpen(true);
  };

  const closeImportDialog = () => {
    if (importing) return;
    setImportOpen(false);
    resetImportState();
  };

  const startNewImport = () => {
    resetImportState();
    if (importFileInput.current) importFileInput.current.value = '';
  };

  const selectImportFile = (file) => {
    if (importing || !file) return;
    setImportResult(null);
    setImportError(null);

    if (!file.name.toLowerCase().endsWith('.xlsx')) {
      setImportFile(null);
      setImportError(t('bulkImportFileType'));
      return;
    }

    if (file.size > maxImportBytes) {
      setImportFile(null);
      setImportError(t('bulkImportFileTooLarge'));
      return;
    }

    setImportFile(file);
  };

  const handleImportDrop = (event) => {
    event.preventDefault();
    setImportDragActive(false);
    if (importing) return;
    selectImportFile(event.dataTransfer.files?.[0]);
  };

  const handleImportFileInput = (event) => {
    const file = event.target.files?.[0];
    event.target.value = '';
    selectImportFile(file);
  };

  const downloadTemplate = async () => {
    setTemplateDownloading(true);
    try {
      const response = await api.get('/chemicals/excel-template', { responseType: 'blob' });
      downloadBlob(response.data, 'kimyasal-toplu-yukleme-sablonu.xlsx');
    } catch {
      setMessage({ type: 'error', text: t('templateDownloadFailed') });
    } finally {
      setTemplateDownloading(false);
    }
  };

  const submitImport = async () => {
    if (!importFile) {
      setImportError(t('bulkImportFileRequired'));
      return;
    }

    setImporting(true);
    setImportError(null);
    setImportResult(null);

    try {
      const body = new FormData();
      body.append('file', importFile);
      const response = await api.post('/chemicals/bulk-import', body);
      setImportResult(response.data);
      if (response.data.successCount > 0) await load();
    } catch (error) {
      const missingHeaders = error.response?.data?.details?.missingHeaders;
      const baseMessage = error.response?.status === 413
        ? t('bulkImportFileTooLarge')
        : getLocalizedApiError(error, t, t('bulkImportFailed'));
      setImportError(missingHeaders?.length ? `${baseMessage} (${missingHeaders.join(', ')})` : baseMessage);
    } finally {
      setImporting(false);
    }
  };

  const create = async (event) => {
    event.preventDefault();
    try {
      await api.post('/chemicals', { ...form, description: form.description || null, msdsUrl: form.msdsUrl || null });
      setForm(emptyForm);
      setMessage({ type: 'success', text: t('chemicalCreated') });
      await load();
    } catch {
      setMessage({ type: 'error', text: t('chemicalCreateFailed') });
    }
  };

  const remove = async (chemical) => {
    if (!window.confirm(`${chemical.chemicalCode} ${t('deleteChemicalConfirm')}`)) return;
    try {
      await api.delete(`/chemicals/${chemical.id}`);
      setMessage({ type: 'success', text: t('chemicalDeleted') });
      await load();
    } catch {
      setMessage({ type: 'error', text: t('chemicalDeleteFailed') });
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
    } catch {
      setChemicals((previousChemicals) => previousChemicals.map((item) => (
        item.id === chemical.id ? { ...item, storageType: previousStorageType } : item
      )));
      setMessage({ type: 'error', text: t('chemicalTypeUpdateFailed') });
    }
  };

  const importSummary = importResult
    ? t('bulkImportSummary')
      .replace('{total}', importResult.totalRows)
      .replace('{success}', importResult.successCount)
      .replace('{failed}', importResult.failedCount)
    : '';
  const importSeverity = importResult?.failedCount === 0
    ? 'success'
    : importResult?.successCount > 0 ? 'warning' : 'error';

  return <Box>
    <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 2, flexWrap: 'wrap', mb: 3 }}>
      <Typography variant="h4" sx={{ fontWeight: 600 }}>{t('chemicalsTitle')}</Typography>
      {canManage && <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
        <Button
          variant="outlined"
          onClick={downloadTemplate}
          disabled={templateDownloading}
          startIcon={templateDownloading ? <CircularProgress size={18} /> : null}
        >
          {t('downloadExcelTemplate')}
        </Button>
        <Button variant="contained" onClick={openImportDialog}>
          {t('bulkImportButton')}
        </Button>
      </Box>}
    </Box>
    {message && <Alert severity={message.type} sx={{ mb: 2 }}>{String(message.text)}</Alert>}
    {canManage && <Paper component="form" onSubmit={create} sx={{ p: 3, mb: 3, display: 'grid', gridTemplateColumns: { xs: '1fr', md: '1fr 2fr 1.5fr 2fr 2fr auto' }, gap: 2 }}>
      <TextField required label={t('chemicalCode')} value={form.chemicalCode} onChange={(e) => setForm({ ...form, chemicalCode: e.target.value })} />
      <TextField required label={t('chemicalName')} value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
      <TextField required select label={t('storageType')} value={form.storageType} onChange={(e) => setForm({ ...form, storageType: e.target.value })}>{storageTypes.map((item) => <MenuItem key={item.value} value={item.value}>{item.label}</MenuItem>)}</TextField>
      <TextField label={t('description')} value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
      <TextField label={t('safetyDataSheetUrl')} value={form.msdsUrl} onChange={(e) => setForm({ ...form, msdsUrl: e.target.value })} />
      <Button type="submit" variant="contained">{t('saveCreate')}</Button>
    </Paper>}
    <Paper><Table><TableHead><TableRow><TableCell>{t('code')}</TableCell><TableCell>{t('name')}</TableCell><TableCell>{t('storageType')}</TableCell><TableCell>{t('description')}</TableCell><TableCell>{t('status')}</TableCell><TableCell /></TableRow></TableHead>
      <TableBody>{chemicals.map((chemical) => <TableRow key={chemical.id}><TableCell>{chemical.chemicalCode}</TableCell><TableCell>{chemical.name}</TableCell><TableCell><TextField select size="small" disabled={!canManage} value={chemical.storageType || 'GENERAL'} onChange={(e) => updateStorageType(chemical, e.target.value)} sx={{ minWidth: 130 }}>{storageTypes.map((item) => <MenuItem key={item.value} value={item.value}>{item.label}</MenuItem>)}</TextField></TableCell><TableCell>{chemical.description || '-'}</TableCell><TableCell>{chemical.stopped ? t('stopped') : t('active')}</TableCell><TableCell align="right">{canManage && <Button color="error" onClick={() => remove(chemical)}>{t('delete')}</Button>}</TableCell></TableRow>)}</TableBody>
    </Table></Paper>

    <Dialog open={importOpen} onClose={closeImportDialog} fullWidth maxWidth="md">
      <DialogTitle>{t('bulkImportTitle')}</DialogTitle>
      <DialogContent dividers>
        {!importResult && <>
          <Alert severity="info" sx={{ mb: 2 }}>{t('bulkImportSampleWarning')}</Alert>
          {importError && <Alert severity="error" sx={{ mb: 2 }}>{importError}</Alert>}
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>{t('bulkImportDescription')}</Typography>
          <Box
            role="button"
            tabIndex={0}
            aria-label={t('bulkImportDrop')}
            onClick={() => { if (!importing) importFileInput.current?.click(); }}
            onKeyDown={(event) => {
              if (!importing && (event.key === 'Enter' || event.key === ' ')) {
                event.preventDefault();
                importFileInput.current?.click();
              }
            }}
            onDragEnter={(event) => { event.preventDefault(); setImportDragActive(true); }}
            onDragOver={(event) => { event.preventDefault(); setImportDragActive(true); }}
            onDragLeave={(event) => { event.preventDefault(); setImportDragActive(false); }}
            onDrop={handleImportDrop}
            sx={{
              border: '2px dashed',
              borderColor: importDragActive ? 'primary.main' : 'divider',
              borderRadius: 2,
              p: { xs: 3, sm: 5 },
              textAlign: 'center',
              cursor: 'pointer',
              bgcolor: importDragActive ? 'action.hover' : 'transparent',
              opacity: importing ? 0.7 : 1,
              pointerEvents: importing ? 'none' : 'auto',
              transition: 'border-color 150ms ease, background-color 150ms ease',
            }}
          >
            <CloudUpload sx={{ fontSize: 48, color: 'text.secondary', mb: 1 }} />
            <Typography variant="h6">{t('bulkImportDrop')}</Typography>
            <Typography variant="body2" color="text.secondary">{t('bulkImportDropHint')}</Typography>
            <Typography variant="caption" color="text.secondary" display="block" sx={{ mt: 1 }}>
              {t('bulkImportFileType')} · 10 MB
            </Typography>
          </Box>
          <input
            ref={importFileInput}
            hidden
            type="file"
            accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            onChange={handleImportFileInput}
          />
          {importFile && <Box sx={{ mt: 2, p: 2, borderRadius: 1, bgcolor: 'action.hover', display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 2 }}>
            <Box sx={{ minWidth: 0 }}>
              <Typography variant="body2" sx={{ fontWeight: 600 }} noWrap>{importFile.name}</Typography>
              <Typography variant="caption" color="text.secondary">{formatFileSize(importFile.size)}</Typography>
            </Box>
            <Button size="small" onClick={() => { setImportFile(null); if (importFileInput.current) importFileInput.current.value = ''; }} disabled={importing}>
              {t('clear')}
            </Button>
          </Box>}
        </>}
        {importResult && <>
          <Alert severity={importSeverity} sx={{ mb: 2 }}>{importSummary}</Alert>
          {importResult.failedCount === 0
            ? <Typography color="text.secondary">{t('bulkImportAllSuccess')}</Typography>
            : <>
              <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1 }}>{t('bulkImportErrorsTitle')}</Typography>
              <TableContainer sx={{ maxHeight: 320 }}>
                <Table stickyHeader size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>{t('importRow')}</TableCell>
                      <TableCell>{t('importField')}</TableCell>
                      <TableCell>{t('importErrorLabel')}</TableCell>
                      <TableCell>{t('importValue')}</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {(importResult.errors || []).map((error, index) => <TableRow key={`${error.rowNumber}-${error.field}-${index}`}>
                      <TableCell>{error.rowNumber}</TableCell>
                      <TableCell>{error.field}</TableCell>
                      <TableCell>{error.message}</TableCell>
                      <TableCell>{error.value || '-'}</TableCell>
                    </TableRow>)}
                  </TableBody>
                </Table>
              </TableContainer>
            </>}
        </>}
      </DialogContent>
      <DialogActions>
        {importResult ? <>
          <Button onClick={startNewImport}>{t('bulkImportNewFile')}</Button>
          <Button variant="contained" onClick={closeImportDialog}>{t('close')}</Button>
        </> : <>
          <Button onClick={closeImportDialog} disabled={importing}>{t('cancel')}</Button>
          <Button
            variant="contained"
            onClick={submitImport}
            disabled={!importFile || importing}
            startIcon={importing ? <CircularProgress size={18} /> : null}
          >
            {importing ? t('bulkImportUploading') : t('bulkImportUpload')}
          </Button>
        </>}
      </DialogActions>
    </Dialog>
  </Box>;
}
