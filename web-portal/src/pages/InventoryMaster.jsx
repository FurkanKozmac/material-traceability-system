import React, { useEffect, useState } from 'react';
import { Alert, Box, Button, MenuItem, Paper, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography } from '@mui/material';
import api from '../api';
import { useLanguage } from '../useLanguage';

export default function InventoryMaster() {
  const { t } = useLanguage();
  const [barcode, setBarcode] = useState('');
  const [addressId, setAddressId] = useState('');
  const [addresses, setAddresses] = useState([]);
  const [units, setUnits] = useState([]);
  const [message, setMessage] = useState(null);

  useEffect(() => {
    api.get('/addresses').then((response) => setAddresses(response.data.items)).catch(() => setMessage({ type: 'error', text: t('unitLoadFailed') }));
  }, [t]);

  const searchUnits = async (event) => {
    event.preventDefault();
    setMessage(null);
    try {
      const response = await api.get('/units', { params: { search: barcode.trim(), size: 200 } });
      setUnits(response.data);
      if (!response.data.length) setMessage({ type: 'info', text: t('unitNotFound') });
    } catch (error) {
      setUnits([]);
      setMessage({ type: 'error', text: error.response?.data || t('unitSearchFailed') });
    }
  };

  const listByAddress = async () => {
    if (!addressId) return;
    setMessage(null);
    try {
      setUnits((await api.get('/units', { params: { addressId, size: 500 } })).data);
    } catch (error) {
      setUnits([]);
      setMessage({ type: 'error', text: error.response?.data || t('rackUnitsLoadFailed') });
    }
  };

  return <Box>
    <Typography variant="h4" sx={{ mb: 3, fontWeight: 600 }}>{t('unitsTitle')}</Typography>
    {message && <Alert severity={message.type} sx={{ mb: 2 }}>{String(message.text)}</Alert>}
    <Paper sx={{ p: 3, mb: 3, display: 'flex', gap: 2, flexWrap: 'wrap' }}>
      <Box component="form" onSubmit={searchUnits} sx={{ display: 'flex', gap: 1, flex: 1 }}>
        <TextField fullWidth required label={t('searchUnit')} placeholder={t('searchUnitPlaceholder')} value={barcode} onChange={(e) => setBarcode(e.target.value)} />
        <Button type="submit" variant="contained">{t('search')}</Button>
        <Button onClick={() => { setBarcode(''); setUnits([]); setMessage(null); }}>{t('clear')}</Button>
      </Box>
      <TextField select label={t('selectRack')} value={addressId} onChange={(e) => setAddressId(e.target.value)} sx={{ minWidth: 260 }}>
        {addresses.map((address) => <MenuItem key={address.id} value={address.id}>{address.code}</MenuItem>)}
      </TextField>
      <Button variant="outlined" onClick={listByAddress} disabled={!addressId}>{t('listRackUnits')}</Button>
    </Paper>
    <Paper><Table><TableHead><TableRow><TableCell>{t('barcode')}</TableCell><TableCell>{t('batch')}</TableCell><TableCell>{t('chemical')}</TableCell><TableCell>{t('status')}</TableCell><TableCell>{t('rack')}</TableCell><TableCell>{t('expirationDate')}</TableCell></TableRow></TableHead>
      <TableBody>{units.map((unit) => <TableRow key={unit.id}><TableCell>{unit.barcode}</TableCell><TableCell>{unit.batchNo}</TableCell><TableCell>{unit.chemicalCode} · {unit.chemicalName}</TableCell><TableCell>{['InStock', 'AVAILABLE'].includes(unit.status) ? t('available') : unit.status === 'Depleted' ? t('consumedStatus') : unit.status}</TableCell><TableCell>{unit.addressCode || t('notAssigned')}</TableCell><TableCell>{unit.expirationDate ? new Date(unit.expirationDate).toLocaleDateString() : '-'}</TableCell></TableRow>)}</TableBody>
    </Table></Paper>
  </Box>;
}
