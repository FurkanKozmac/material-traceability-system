import React, { useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Alert, SafeAreaView, ScrollView, StyleSheet, Text, TextInput, TouchableOpacity, View, Vibration } from 'react-native';
import { CameraView, useCameraPermissions } from 'expo-camera';
import { useRouter } from 'expo-router';
import { consumeUnit, scanBatch, scanUnit } from '../src/services/api';
import { useLanguage } from '../src/context/LanguageContext';

export default function ScannerScreen() {
  const router = useRouter();
  const { t } = useLanguage();
  const [permission, requestPermission] = useCameraPermissions();
  const [barcode, setBarcode] = useState('');
  const [unit, setUnit] = useState(null);
  const [activeBatch, setActiveBatch] = useState(null);
  const [scanned, setScanned] = useState(false);
  const [loading, setLoading] = useState(false);
  const scanLockRef = useRef(false);
  const cooldownRef = useRef(null);

  const releaseScanLock = () => {
    if (cooldownRef.current) clearTimeout(cooldownRef.current);
    cooldownRef.current = setTimeout(() => {
      scanLockRef.current = false;
      cooldownRef.current = null;
    }, 2500);
  };

  useEffect(() => () => {
    if (cooldownRef.current) clearTimeout(cooldownRef.current);
  }, []);

  const lookup = async (value, fromCamera = false) => {
    const normalized = value.trim();
    if (!normalized || loading) return;
    if (!/^(BAT-|UNIT-|BAR-)/i.test(normalized)) {
      Alert.alert(t('invalidBarcode'), t('invalidBarcodeMessage'));
      setScanned(false);
      setUnit(null);
      if (fromCamera) releaseScanLock();
      return;
    }
    setLoading(true);
    try {
      if (/^BAT-/i.test(normalized)) {
        const batch = await scanBatch(normalized);
        setActiveBatch(batch);
        setUnit(null);
        setBarcode('');
        setScanned(true);
        Vibration.vibrate([0, 100, 50, 100]);
        return;
      }

      if (!activeBatch) {
        Alert.alert(t('scanBatchFirst'), t('scanBatchFirstMessage'));
        setScanned(false);
        if (fromCamera) releaseScanLock();
        return;
      }

      const result = await scanUnit(normalized);
      if (result.batchId !== activeBatch.id) {
        Alert.alert(t('wrongBatch'), `${t('batch')}: ${activeBatch.batchNo}`);
        setUnit(null);
        setScanned(false);
        if (fromCamera) releaseScanLock();
        return;
      }
      setBarcode(normalized);
      setUnit(result);
      setScanned(true);
      Vibration.vibrate(100);
    } catch (error) {
      setUnit(null);
      setScanned(false);
      if (fromCamera) releaseScanLock();
      Alert.alert(t('barcodeNotFound'), error.message || t('barrelLookupFailed'));
    } finally {
      setLoading(false);
    }
  };

  const handleCameraScan = ({ data }) => {
    if (scanLockRef.current || loading) return;
    scanLockRef.current = true;
    setScanned(true);
    lookup(data, true);
  };

  const consume = async () => {
    if (!unit || loading) return;
    setLoading(true);
    try {
      const result = await consumeUnit(unit.barcode);
      setUnit(result);
      setActiveBatch(await scanBatch(activeBatch.batchBarcode));
      Vibration.vibrate([0, 100, 50, 100]);
      Alert.alert(t('success'), t('consumeSuccess'));
    } catch (error) {
      Alert.alert(t('consumeFailed'), error.message || t('consumeFailed'));
    } finally {
      setLoading(false);
    }
  };

  return <SafeAreaView style={styles.container}>
    <ScrollView contentContainerStyle={styles.content}>
      <View style={styles.header}>
        <Text style={styles.title}>{t('barrelScan')}</Text>
        <TouchableOpacity onPress={() => router.back()}><Text style={styles.back}>{t('back')}</Text></TouchableOpacity>
      </View>

      <View style={styles.searchRow}>
        <TextInput style={styles.input} placeholder={activeBatch ? 'BAR-LOT2026-001' : 'BAT-LOT2026'} autoCapitalize="characters" value={barcode} onChangeText={setBarcode} onSubmitEditing={() => lookup(barcode)} />
        <TouchableOpacity style={styles.searchButton} onPress={() => lookup(barcode)} disabled={loading}>
          <Text style={styles.buttonText}>{t('query')}</Text>
        </TouchableOpacity>
      </View>

      {activeBatch && <View style={styles.batchCard}>
        <View style={styles.batchHeader}>
          <View>
            <Text style={styles.batchEyebrow}>{t('activeBatch')}</Text>
            <Text style={styles.batchTitle}>{activeBatch.batchNo}</Text>
          </View>
          <TouchableOpacity onPress={() => { setActiveBatch(null); setUnit(null); setScanned(false); scanLockRef.current = false; }}><Text style={styles.changeBatch}>{t('change')}</Text></TouchableOpacity>
        </View>
        <Text style={styles.batchChemical}>{activeBatch.chemicalName}</Text>
        <Text style={styles.batchMeta}>{t('rack')}: {activeBatch.addressCodes?.join(', ') || t('unassignedRack')}</Text>
        <View style={styles.batchStats}>
          <Text style={styles.batchStat}>{t('total')}: {activeBatch.totalUnits}</Text>
          <Text style={styles.batchStatAvailable}>{t('available')}: {activeBatch.availableUnits}</Text>
          <Text style={styles.batchStat}>{t('consumed')}: {activeBatch.consumedUnits}</Text>
        </View>
        {scanned && !unit && <TouchableOpacity style={styles.continueButton} onPress={() => { setScanned(false); scanLockRef.current = false; }}><Text style={styles.buttonText}>{t('continueUnitScan')}</Text></TouchableOpacity>}
      </View>}

      {permission?.granted ? <View style={styles.cameraBox}>
        <CameraView style={StyleSheet.absoluteFillObject} barcodeScannerSettings={{ barcodeTypes: ['qr'] }} onBarcodeScanned={scanned ? undefined : handleCameraScan} />
        <View style={styles.reticle} />
      </View> : <TouchableOpacity style={styles.permissionButton} onPress={requestPermission}>
        <Text style={styles.buttonText}>{t('enableCamera')}</Text>
      </TouchableOpacity>}

      {loading && <ActivityIndicator size="large" color="#2563eb" style={styles.loader} />}

      {unit && <View style={styles.card}>
        <Text style={styles.cardTitle}>{unit.chemicalName}</Text>
        <Text style={styles.row}>{t('chemicalCode')}: {unit.chemicalCode}</Text>
        <Text style={styles.row}>{t('batch')}: {unit.batchNo}</Text>
        <Text style={styles.row}>{t('barcode')}: {unit.barcode}</Text>
        <Text style={styles.row}>{t('rack')}: {unit.addressCode || t('unassignedRack')}</Text>
        <Text style={styles.row}>{t('expiration')}: {unit.expirationDate ? new Date(unit.expirationDate).toLocaleDateString() : '-'}</Text>
        <Text style={styles.row}>{t('status')}: {unit.status}</Text>

        <TouchableOpacity style={[styles.consumeButton, unit.status === 'Depleted' && styles.disabled]} onPress={consume} disabled={loading || unit.status === 'Depleted'}>
          <Text style={styles.consumeText}>{unit.status === 'Depleted' ? t('depleted') : t('consume')}</Text>
        </TouchableOpacity>
        <TouchableOpacity style={styles.scanAgain} onPress={() => { setUnit(null); setBarcode(''); setScanned(false); scanLockRef.current = false; }}>
          <Text style={styles.scanAgainText}>{t('sameBatchScan')}</Text>
        </TouchableOpacity>
      </View>}
    </ScrollView>
  </SafeAreaView>;
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#f8fafc' },
  content: { padding: 20, gap: 18 },
  header: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  title: { fontSize: 24, fontWeight: '700', color: '#111827' },
  back: { color: '#2563eb', fontWeight: '600' },
  searchRow: { flexDirection: 'row', gap: 10 },
  input: { flex: 1, backgroundColor: '#fff', borderWidth: 1, borderColor: '#d1d5db', borderRadius: 10, padding: 14, fontSize: 16 },
  searchButton: { backgroundColor: '#2563eb', borderRadius: 10, paddingHorizontal: 20, justifyContent: 'center' },
  permissionButton: { backgroundColor: '#2563eb', borderRadius: 10, padding: 16, alignItems: 'center' },
  batchCard: { backgroundColor: '#eff6ff', borderWidth: 1, borderColor: '#bfdbfe', borderRadius: 14, padding: 16 },
  batchHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  batchEyebrow: { color: '#1d4ed8', fontSize: 11, fontWeight: '800', letterSpacing: 0.6 },
  batchTitle: { color: '#0f172a', fontSize: 22, fontWeight: '800', marginTop: 2 },
  batchChemical: { color: '#334155', fontSize: 16, fontWeight: '600', marginTop: 8 },
  batchMeta: { color: '#64748b', marginTop: 4 },
  batchStats: { flexDirection: 'row', gap: 12, marginTop: 12 },
  batchStat: { color: '#475569', fontWeight: '600' },
  batchStatAvailable: { color: '#15803d', fontWeight: '800' },
  changeBatch: { color: '#2563eb', fontWeight: '700' },
  continueButton: { backgroundColor: '#2563eb', borderRadius: 10, padding: 13, alignItems: 'center', marginTop: 14 },
  buttonText: { color: '#fff', fontWeight: '700' },
  cameraBox: { height: 300, borderRadius: 16, overflow: 'hidden', backgroundColor: '#111827', alignItems: 'center', justifyContent: 'center' },
  reticle: { width: 210, height: 210, borderWidth: 3, borderColor: '#22c55e', borderRadius: 14 },
  loader: { marginVertical: 12 },
  card: { backgroundColor: '#fff', padding: 22, borderRadius: 16, borderWidth: 1, borderColor: '#e5e7eb', gap: 8 },
  cardTitle: { fontSize: 22, fontWeight: '700', color: '#111827', marginBottom: 8 },
  row: { fontSize: 16, color: '#374151' },
  consumeButton: { backgroundColor: '#16a34a', borderRadius: 12, padding: 18, alignItems: 'center', marginTop: 14 },
  consumeText: { color: '#fff', fontSize: 17, fontWeight: '800' },
  disabled: { backgroundColor: '#9ca3af' },
  scanAgain: { padding: 12, alignItems: 'center' },
  scanAgainText: { color: '#2563eb', fontWeight: '600' },
});
