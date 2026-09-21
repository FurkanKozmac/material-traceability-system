import React, { useEffect, useState } from 'react';
import { Alert, Box, Button, Card, CardActions, CardContent, Chip, Grid, MenuItem, Paper, Stack, Tab, Tabs, TextField, Typography } from '@mui/material';
import { AddRoad, CancelOutlined, SwapHoriz } from '@mui/icons-material';
import api from '../api';
import { isAdmin } from '../auth';

const labels = { Pending: 'BEKLİYOR', Completed: 'TAMAMLANDI', Cancelled: 'İPTAL EDİLDİ' };

export default function RelocationTasks() {
  const canManage = isAdmin();
  const [tasks, setTasks] = useState([]);
  const [status, setStatus] = useState('Pending');
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState(null);
  const [barcode, setBarcode] = useState('');
  const [unit, setUnit] = useState(null);
  const [targets, setTargets] = useState([]);
  const [targetId, setTargetId] = useState('');

  const load = async () => {
    try { setTasks((await api.get('/relocation-tasks')).data); }
    catch (error) { setMessage({ type: 'error', text: error.response?.data?.message || 'Görevler yüklenemedi.' }); }
    finally { setLoading(false); }
  };

  useEffect(() => { load(); const timer = setInterval(load, 5000); return () => clearInterval(timer); }, []);

  const findUnit = async (event) => {
    event.preventDefault(); setMessage(null); setUnit(null); setTargets([]); setTargetId('');
    try {
      const found = (await api.get(`/units/barcode/${encodeURIComponent(barcode.trim())}`)).data;
      const available = (await api.get(`/relocation-tasks/eligible-targets/${found.id}`)).data;
      setUnit(found); setTargets(available);
      if (!available.length) setMessage({ type: 'warning', text: 'Uyumlu ve kapasitesi müsait başka raf bulunamadı.' });
    } catch (error) { setMessage({ type: 'error', text: error.response?.data?.message || error.response?.data || 'Birim sorgulanamadı.' }); }
  };

  const create = async () => {
    try {
      await api.post('/relocation-tasks', { unitId: unit.id, toAddressId: Number(targetId) });
      setMessage({ type: 'success', text: 'Görev oluşturuldu. Mobil terminalde Unit ve hedef raf okutularak tamamlanmalıdır.' });
      setBarcode(''); setUnit(null); setTargets([]); setTargetId(''); setStatus('Pending'); await load();
    } catch (error) { setMessage({ type: 'error', text: error.response?.data?.message || 'Görev oluşturulamadı.' }); }
  };

  const cancel = async (id) => {
    try { await api.put(`/relocation-tasks/${id}/cancel`); await load(); }
    catch (error) { setMessage({ type: 'error', text: error.response?.data?.message || 'Görev iptal edilemedi.' }); }
  };

  const visible = tasks.filter((task) => task.status === status);
  return <Box sx={{ maxWidth: 1280, mx: 'auto', pb: 5 }}>
    <Typography variant="h4" sx={{ fontWeight: 700 }}>Raf Taşıma Görevleri</Typography>
    <Typography color="text.secondary" sx={{ mt: .5, mb: 3 }}>Görevi planlayın; fiziksel taşımayı mobil terminalde çift QR doğrulamasıyla tamamlayın.</Typography>
    {message && <Alert severity={message.type} sx={{ mb: 2 }}>{String(message.text)}</Alert>}
    {canManage && <Paper variant="outlined" sx={{ p: 2.5, mb: 3, borderRadius: 3 }}>
      <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 2 }}><AddRoad color="primary" /><Typography variant="h6" fontWeight={700}>Yeni Görev</Typography></Stack>
      <Box component="form" onSubmit={findUnit} sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap' }}>
        <TextField required label="Stok Birimi Barkodu" value={barcode} onChange={(e) => setBarcode(e.target.value)} sx={{ flex: '1 1 300px' }} />
        <Button type="submit" variant="outlined">Birimi Bul</Button>
      </Box>
      {unit && <Box sx={{ mt: 2, p: 2, bgcolor: '#f8fafc', borderRadius: 2, display: 'grid', gridTemplateColumns: { xs: '1fr', md: '1fr 1.5fr auto' }, gap: 2, alignItems: 'center' }}>
        <Box><Typography fontWeight={750}>{unit.barcode}</Typography><Typography variant="body2">{unit.chemicalName} · Mevcut raf: {unit.addressCode || '-'}</Typography></Box>
        <TextField select label="Uyumlu Hedef Raf" value={targetId} onChange={(e) => setTargetId(e.target.value)} disabled={!targets.length}>
          {targets.map((target) => <MenuItem key={target.id} value={target.id}>{target.code} · {target.storageType} · {target.availableCapacity == null ? 'Sınırsız' : `${target.availableCapacity} boş yer`}</MenuItem>)}
        </TextField>
        <Button variant="contained" disabled={!targetId} onClick={create}>Görev Oluştur</Button>
      </Box>}
    </Paper>}
    <Paper variant="outlined" sx={{ borderRadius: 3, overflow: 'hidden' }}>
      <Tabs value={status} onChange={(_, value) => setStatus(value)} sx={{ borderBottom: '1px solid #e2e8f0' }}>
        {['Pending', 'Completed', 'Cancelled'].map((value) => <Tab key={value} value={value} label={`${labels[value]} (${tasks.filter((x) => x.status === value).length})`} />)}
      </Tabs>
      <Box sx={{ p: 2.5 }}>
        {loading ? <Typography>Yükleniyor…</Typography> : !visible.length ? <Typography sx={{ py: 5, textAlign: 'center', color: '#64748b' }}>Bu durumda görev bulunmuyor.</Typography> : <Grid container spacing={2}>
          {visible.map((task) => <Grid size={{ xs: 12, sm: 6, lg: 4 }} key={task.id}><Card variant="outlined" sx={{ borderRadius: 2.5 }}><CardContent>
            <Stack direction="row" justifyContent="space-between"><Typography fontWeight={700}>Görev #{task.id}</Typography><Chip size="small" label={labels[task.status]} /></Stack>
            <Typography sx={{ mt: 2, fontFamily: 'monospace', fontWeight: 800 }}>{task.unitBarcode}</Typography>
            <Box sx={{ mt: 2, p: 1.5, bgcolor: '#f8fafc', display: 'flex', justifyContent: 'space-around', alignItems: 'center' }}><Box textAlign="center"><small>Kaynak</small><Typography fontWeight={800}>{task.fromAddressCode}</Typography></Box><SwapHoriz /><Box textAlign="center"><small>Hedef</small><Typography fontWeight={800}>{task.toAddressCode}</Typography></Box></Box>
            <Typography variant="caption" color="text.secondary">{new Date(task.createdAt).toLocaleString('tr-TR')}</Typography>
          </CardContent>{task.status === 'Pending' && <CardActions>{canManage && <Button color="error" startIcon={<CancelOutlined />} onClick={() => cancel(task.id)}>İptal Et</Button>}<Typography variant="caption" sx={{ ml: 'auto' }}>Mobilde tamamlanır</Typography></CardActions>}</Card></Grid>)}
        </Grid>}
      </Box>
    </Paper>
  </Box>;
}
