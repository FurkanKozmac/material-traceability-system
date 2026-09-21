import React, { useEffect, useState } from 'react';
import { Alert, Box, Button, MenuItem, Paper, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography } from '@mui/material';
import QRCode from 'qrcode';
import api from '../api';
import { isAdmin } from '../auth';

export default function AddressManagement() {
  const canManage = isAdmin();
  const [addresses, setAddresses] = useState([]);
  const storageTypes = [
    { value: 'GENERAL', label: 'Genel' },
    { value: 'FLAMMABLE', label: 'Yanıcı' },
    { value: 'CORROSIVE', label: 'Korozif' },
    { value: 'PAINT', label: 'Boya' },
  ];
  const storageTypeLabel = (value) => storageTypes.find((item) => item.value === value)?.label || value;
  const [form, setForm] = useState({ code: '', maxCapacity: '', storageType: 'GENERAL' });
  const [message, setMessage] = useState(null);
  const load = async () => setAddresses((await api.get('/addresses')).data);

  useEffect(() => { load().catch(() => setMessage({ type: 'error', text: 'Depo rafları yüklenemedi.' })); }, []);

  const create = async (event) => {
    event.preventDefault();
    try {
      await api.post('/addresses', { code: form.code, maxCapacity: form.maxCapacity ? Number(form.maxCapacity) : null, storageType: form.storageType });
      setForm({ code: '', maxCapacity: '', storageType: 'GENERAL' });
      setMessage({ type: 'success', text: 'Depo rafı oluşturuldu.' });
      await load();
    } catch (error) {
      setMessage({ type: 'error', text: error.response?.data?.message || error.response?.data || 'Depo rafı oluşturulamadı.' });
    }
  };

  const printAddressLabel = async (address) => {
    const popup = window.open('', '_blank', 'width=520,height=620');
    if (!popup) { setMessage({ type: 'warning', text: 'Etiketi yazdırmak için açılır pencerelere izin veriniz.' }); return; }
    const payload = `ADR-${address.code}`;
    const qr = await QRCode.toDataURL(payload, { width: 360, margin: 1, errorCorrectionLevel: 'M' });
    popup.document.write(`<!doctype html><html><head><title>${payload}</title><style>@page{size:A4;margin:12mm}body{font-family:Arial;display:grid;place-items:start center}.label{width:90mm;border:2px solid #000;padding:8mm;text-align:center;break-inside:avoid}.label img{width:55mm;height:55mm}.code{font:900 24px monospace;margin:4mm 0}.meta{font-size:15px}</style></head><body><div class="label"><div>HEDEF RAF ETİKETİ</div><img src="${qr}"/><div class="code">${payload}</div><div class="meta">${storageTypeLabel(address.storageType)} Depolama · Kapasite: ${address.maxCapacity ?? 'Sınırsız'}</div></div><script>onload=()=>{print();onafterprint=()=>close();}</script></body></html>`);
    popup.document.close();
  };

  const updateStorageType = async (address, storageType) => {
    try {
      await api.put(`/addresses/${address.id}/storage-type`, { storageType });
      setMessage({ type: 'success', text: `${address.code} depolama türü güncellendi.` });
      await load();
    } catch (error) {
      setMessage({ type: 'error', text: error.response?.data?.message || 'Depolama türü güncellenemedi.' });
    }
  };

  return <Box>
    <Typography variant="h4" sx={{ mb: 3, fontWeight: 600 }}>Depo Rafları</Typography>
    {message && <Alert severity={message.type} sx={{ mb: 2 }}>{String(message.text)}</Alert>}
    {canManage && <Paper component="form" onSubmit={create} sx={{ p: 3, mb: 3, display: 'flex', gap: 2, flexWrap: 'wrap' }}>
      <TextField required label="Raf Kodu" value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} />
      <TextField type="number" label="Kapasite" slotProps={{ htmlInput: { min: 1, max: 1000 } }} value={form.maxCapacity} onChange={(e) => setForm({ ...form, maxCapacity: e.target.value })} />
      <TextField required select label="Depolama Türü" value={form.storageType} onChange={(e) => setForm({ ...form, storageType: e.target.value })} sx={{ minWidth: 180 }}>{storageTypes.map((item) => <MenuItem key={item.value} value={item.value}>{item.label}</MenuItem>)}</TextField>
      <Button type="submit" variant="contained">Kaydet / Oluştur</Button>
    </Paper>}
    <Paper><Table><TableHead><TableRow><TableCell>Raf Kodu</TableCell><TableCell>Depolama Türü</TableCell><TableCell>Kapasite</TableCell><TableCell>Mevcut Doluluk</TableCell><TableCell>Boş Kapasite</TableCell><TableCell>Etiket</TableCell></TableRow></TableHead>
      <TableBody>{addresses.map((address) => <TableRow key={address.id}><TableCell>{address.code}</TableCell><TableCell><TextField select size="small" disabled={!canManage} value={address.storageType} onChange={(e) => updateStorageType(address, e.target.value)} sx={{ minWidth: 130 }}>{storageTypes.map((item) => <MenuItem key={item.value} value={item.value}>{item.label}</MenuItem>)}</TextField></TableCell><TableCell>{address.maxCapacity ?? 'Sınırsız'}</TableCell><TableCell>{address.currentOccupancy}</TableCell><TableCell>{address.maxCapacity == null ? 'Sınırsız' : Math.max(0, address.maxCapacity - address.currentOccupancy)}</TableCell><TableCell><Button size="small" onClick={() => printAddressLabel(address)}>QR Yazdır</Button></TableCell></TableRow>)}</TableBody>
    </Table></Paper>
  </Box>;
}
