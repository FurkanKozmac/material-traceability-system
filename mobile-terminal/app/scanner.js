import React, { useState } from 'react';
import { ActivityIndicator, Alert, SafeAreaView, ScrollView, StyleSheet, Text, TextInput, TouchableOpacity, View, Vibration } from 'react-native';
import { CameraView, useCameraPermissions } from 'expo-camera';
import { useRouter } from 'expo-router';
import { consumeUnit, scanBatch, scanUnit } from '../src/services/api';

export default function ScannerScreen() {
  const router = useRouter();
  const [permission, requestPermission] = useCameraPermissions();
  const [barcode, setBarcode] = useState('');
  const [unit, setUnit] = useState(null);
  const [activeBatch, setActiveBatch] = useState(null);
  const [scanned, setScanned] = useState(false);
  const [loading, setLoading] = useState(false);

  const lookup = async (value) => {
    const normalized = value.trim();
    if (!normalized || loading) return;
    if (!/^(BAT-|UNIT-|BAR-)/i.test(normalized)) {
      Alert.alert('Geçersiz Barkod', 'Geçersiz MTS Barkodu! Lütfen sistemimize ait bir batch veya varil etiketi okutunuz.');
      setScanned(false);
      setUnit(null);
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
        Alert.alert('Önce Batch Okutulmalı', 'Varil tüketimine başlamadan önce hammadde partisinin BAT- ile başlayan QR etiketini okutunuz.');
        setScanned(false);
        return;
      }

      const result = await scanUnit(normalized);
      if (result.batchId !== activeBatch.id) {
        Alert.alert('Yanlış Hammadde Partisi', `Okutulan varil açık olan ${activeBatch.batchNo} partisine ait değildir.`);
        setUnit(null);
        setScanned(false);
        return;
      }
      setBarcode(normalized);
      setUnit(result);
      setScanned(true);
      Vibration.vibrate(100);
    } catch (error) {
      setUnit(null);
      setScanned(false);
      Alert.alert('Barkod bulunamadı', error.message || 'Varil bilgisi alınamadı.');
    } finally {
      setLoading(false);
    }
  };

  const consume = async () => {
    if (!unit || loading) return;
    setLoading(true);
    try {
      const result = await consumeUnit(unit.barcode);
      setUnit(result);
      setActiveBatch(await scanBatch(activeBatch.batchBarcode));
      Vibration.vibrate([0, 100, 50, 100]);
      Alert.alert('Başarılı', 'Varil başarıyla tüketime verildi!');
    } catch (error) {
      Alert.alert('Tüketim başarısız', error.message || 'Varil tüketime verilemedi.');
    } finally {
      setLoading(false);
    }
  };

  return <SafeAreaView style={styles.container}>
    <ScrollView contentContainerStyle={styles.content}>
      <View style={styles.header}>
        <Text style={styles.title}>Varil Barkod İşlemi</Text>
        <TouchableOpacity onPress={() => router.back()}><Text style={styles.back}>Geri</Text></TouchableOpacity>
      </View>

      <View style={styles.searchRow}>
        <TextInput style={styles.input} placeholder={activeBatch ? 'BAR-LOT2026-001' : 'BAT-LOT2026'} autoCapitalize="characters" value={barcode} onChangeText={setBarcode} onSubmitEditing={() => lookup(barcode)} />
        <TouchableOpacity style={styles.searchButton} onPress={() => lookup(barcode)} disabled={loading}>
          <Text style={styles.buttonText}>Sorgula</Text>
        </TouchableOpacity>
      </View>

      {activeBatch && <View style={styles.batchCard}>
        <View style={styles.batchHeader}>
          <View>
            <Text style={styles.batchEyebrow}>AKTİF HAMMADDE PARTİSİ</Text>
            <Text style={styles.batchTitle}>{activeBatch.batchNo}</Text>
          </View>
          <TouchableOpacity onPress={() => { setActiveBatch(null); setUnit(null); setScanned(false); }}><Text style={styles.changeBatch}>Değiştir</Text></TouchableOpacity>
        </View>
        <Text style={styles.batchChemical}>{activeBatch.chemicalName}</Text>
        <Text style={styles.batchMeta}>Raf: {activeBatch.addressCodes?.join(', ') || 'Rafa atanmamış'}</Text>
        <View style={styles.batchStats}>
          <Text style={styles.batchStat}>Toplam: {activeBatch.totalUnits}</Text>
          <Text style={styles.batchStatAvailable}>Müsait: {activeBatch.availableUnits}</Text>
          <Text style={styles.batchStat}>Tüketilen: {activeBatch.consumedUnits}</Text>
        </View>
        {scanned && !unit && <TouchableOpacity style={styles.continueButton} onPress={() => setScanned(false)}><Text style={styles.buttonText}>BİRİM TARAMAYA GEÇ</Text></TouchableOpacity>}
      </View>}

      {permission?.granted ? <View style={styles.cameraBox}>
        <CameraView style={StyleSheet.absoluteFillObject} barcodeScannerSettings={{ barcodeTypes: ['qr'] }} onBarcodeScanned={scanned ? undefined : ({ data }) => lookup(data)} />
        <View style={styles.reticle} />
      </View> : <TouchableOpacity style={styles.permissionButton} onPress={requestPermission}>
        <Text style={styles.buttonText}>Kamerayı Etkinleştir</Text>
      </TouchableOpacity>}

      {loading && <ActivityIndicator size="large" color="#2563eb" style={styles.loader} />}

      {unit && <View style={styles.card}>
        <Text style={styles.cardTitle}>{unit.chemicalName}</Text>
        <Text style={styles.row}>Kimyasal Kodu: {unit.chemicalCode}</Text>
        <Text style={styles.row}>Parti: {unit.batchNo}</Text>
        <Text style={styles.row}>Barkod: {unit.barcode}</Text>
        <Text style={styles.row}>Raf: {unit.addressCode || 'Rafta değil'}</Text>
        <Text style={styles.row}>SKT: {unit.expirationDate ? new Date(unit.expirationDate).toLocaleDateString() : '-'}</Text>
        <Text style={styles.row}>Durum: {unit.status}</Text>

        <TouchableOpacity style={[styles.consumeButton, unit.status === 'Depleted' && styles.disabled]} onPress={consume} disabled={loading || unit.status === 'Depleted'}>
          <Text style={styles.consumeText}>{unit.status === 'Depleted' ? 'TÜKETİLMİŞ' : 'TÜKETİME VER (CONSUME)'}</Text>
        </TouchableOpacity>
        <TouchableOpacity style={styles.scanAgain} onPress={() => { setUnit(null); setBarcode(''); setScanned(false); }}>
          <Text style={styles.scanAgainText}>Aynı Partiden Yeni Birim Tara</Text>
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
