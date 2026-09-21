import React, { useEffect, useState } from 'react';
import { Alert, Box, Button, MenuItem, Paper, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography } from '@mui/material';
import api from '../api';

export default function InventoryMaster() {
  const [barcode, setBarcode] = useState('');
  const [addressId, setAddressId] = useState('');
  const [addresses, setAddresses] = useState([]);
  const [units, setUnits] = useState([]);
  const [message, setMessage] = useState(null);

  useEffect(() => {
    api.get('/addresses').then((response) => setAddresses(response.data)).catch(() => setMessage({ type: 'error', text: 'Depo rafları yüklenemedi.' }));
  }, []);

  const searchUnits = async (event) => {
    event.preventDefault();
    setMessage(null);
    try {
      const response = await api.get('/units', { params: { search: barcode.trim(), size: 200 } });
      setUnits(response.data);
      if (!response.data.length) setMessage({ type: 'info', text: 'Aramanızla eşleşen stok birimi bulunamadı.' });
    } catch (error) {
      setUnits([]);
      setMessage({ type: 'error', text: error.response?.data || 'Stok birimi bulunamadı.' });
    }
  };

  const listByAddress = async () => {
    if (!addressId) return;
    setMessage(null);
    try {
      setUnits((await api.get('/units', { params: { addressId, size: 500 } })).data);
    } catch (error) {
      setUnits([]);
      setMessage({ type: 'error', text: error.response?.data || 'Raf stoğu yüklenemedi.' });
    }
  };

  return <Box>
    <Typography variant="h4" sx={{ mb: 3, fontWeight: 600 }}>Stok Birimleri</Typography>
    {message && <Alert severity={message.type} sx={{ mb: 2 }}>{String(message.text)}</Alert>}
    <Paper sx={{ p: 3, mb: 3, display: 'flex', gap: 2, flexWrap: 'wrap' }}>
      <Box component="form" onSubmit={searchUnits} sx={{ display: 'flex', gap: 1, flex: 1 }}>
        <TextField fullWidth required label="Stok Birimi Ara" placeholder="Barkod, parti, kimyasal veya raf kodu" value={barcode} onChange={(e) => setBarcode(e.target.value)} />
        <Button type="submit" variant="contained">Ara</Button>
        <Button onClick={() => { setBarcode(''); setUnits([]); setMessage(null); }}>Temizle</Button>
      </Box>
      <TextField select label="Depo Rafı Seçiniz" value={addressId} onChange={(e) => setAddressId(e.target.value)} sx={{ minWidth: 260 }}>
        {addresses.map((address) => <MenuItem key={address.id} value={address.id}>{address.code}</MenuItem>)}
      </TextField>
      <Button variant="outlined" onClick={listByAddress} disabled={!addressId}>Raftakileri Listele</Button>
    </Paper>
    <Paper><Table><TableHead><TableRow><TableCell>Barkod</TableCell><TableCell>Parti</TableCell><TableCell>Kimyasal</TableCell><TableCell>Durum</TableCell><TableCell>Raf</TableCell><TableCell>Son Kullanma Tarihi</TableCell></TableRow></TableHead>
      <TableBody>{units.map((unit) => <TableRow key={unit.id}><TableCell>{unit.barcode}</TableCell><TableCell>{unit.batchNo}</TableCell><TableCell>{unit.chemicalCode} · {unit.chemicalName}</TableCell><TableCell>{['InStock', 'AVAILABLE'].includes(unit.status) ? 'Müsait' : unit.status === 'Depleted' ? 'Tüketildi' : unit.status}</TableCell><TableCell>{unit.addressCode || 'Rafa Atanmamış'}</TableCell><TableCell>{unit.expirationDate ? new Date(unit.expirationDate).toLocaleDateString('tr-TR') : '-'}</TableCell></TableRow>)}</TableBody>
    </Table></Paper>
  </Box>;
}
