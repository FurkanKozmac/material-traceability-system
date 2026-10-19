import React, { useCallback, useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Alert, Image, SafeAreaView, ScrollView, StyleSheet, Text, TextInput, TouchableOpacity, View } from 'react-native';
import { CameraView, useCameraPermissions } from 'expo-camera';
import { useFocusEffect, useRouter } from 'expo-router';
import { createBatch, getLabelPreviewUrl, listChemicals, scanBatchLabel } from '../src/services/api';
import { useLanguage } from '../src/context/LanguageContext';

const asDateInput = (value) => value ? new Date(value).toISOString().slice(0, 10) : '';

export default function ReceiptScreen() {
  const router = useRouter();
  const [isFocused, setIsFocused] = useState(false);
  const { t } = useLanguage();
  const cameraRef = useRef(null);
  const [permission, requestPermission] = useCameraPermissions();
  const [chemicals, setChemicals] = useState([]);
  const [photo, setPhoto] = useState(null);
  const [scanComplete, setScanComplete] = useState(false);
  const [chemicalId, setChemicalId] = useState('');
  const [batchNo, setBatchNo] = useState('');
  const [supplier, setSupplier] = useState('');
  const [expirationDate, setExpirationDate] = useState('');
  const [quantity, setQuantity] = useState(1);
  const [loading, setLoading] = useState(false);
  const [created, setCreated] = useState(null);
  const [cameraReady, setCameraReady] = useState(false);
  const [cameraError, setCameraError] = useState('');

  useFocusEffect(useCallback(() => {
    setIsFocused(true);
    return () => setIsFocused(false);
  }, []));

  useEffect(() => {
    listChemicals().then((items) => setChemicals(items.filter((item) => !item.stopped)))
      .catch(() => Alert.alert(t('receiptError'), t('chemicalLoadFailed')));
  }, []);

  const capturePhoto = async () => {
    if (!cameraRef.current || loading) return;
    try {
      const captured = await cameraRef.current.takePictureAsync({ quality: 0.8 });
      setPhoto({ uri: captured.uri, name: 'label.jpg', type: 'image/jpeg' });
      setScanComplete(false);
    } catch (error) {
      Alert.alert(t('receiptError'), t('receiptOcrFailed'));
    }
  };

  const scanPhoto = async () => {
    if (!photo || loading) return;
    setLoading(true);
    try {
      const result = await scanBatchLabel(photo);
      setScanComplete(true);
      setBatchNo(result.extractedLotNumber || '');
      setExpirationDate(asDateInput(result.extractedExpirationDate));
      const found = chemicals.find((item) => item.id === result.suggestedChemicalId) ||
        chemicals.find((item) => item.name?.toLowerCase() === result.suggestedChemicalName?.toLowerCase());
      if (found) setChemicalId(String(found.id));
      Alert.alert(t('receiptOcrComplete'), found ? t('receiptReviewFields') : t('receiptChemicalManual'));
    } catch (error) {
      Alert.alert(t('receiptError'), t('receiptOcrFailed'));
    } finally {
      setLoading(false);
    }
  };

  const submitReceipt = async () => {
    if (!batchNo.trim() || !chemicalId) {
      Alert.alert(t('receiptMissingFields'), t('receiptMissingFieldsHelp'));
      return;
    }
    setLoading(true);
    try {
      const batch = await createBatch({
        batchNo: batchNo.trim(),
        supplier: supplier.trim() || null,
        initialQuantity: quantity,
        expirationDate: expirationDate ? `${expirationDate}T00:00:00Z` : null,
        chemicalId: Number(chemicalId),
      });
      const previewUrl = await getLabelPreviewUrl(`BAT-${batch.batchNo}`);
      setCreated({ batch, previewUrl });
    } catch (error) {
      Alert.alert(t('receiptError'), t('receiptCreateFailed'));
    } finally {
      setLoading(false);
    }
  };

  const resetReceipt = () => {
    setPhoto(null); setScanComplete(false); setChemicalId(''); setBatchNo(''); setSupplier('');
    setExpirationDate(''); setQuantity(1); setCreated(null);
  };

  return <SafeAreaView style={styles.container}>
    <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
      <View style={styles.header}>
        <Text style={styles.title}>{t('goodsReceipt')}</Text>
        <TouchableOpacity onPress={() => router.back()}><Text style={styles.back}>{t('back')}</Text></TouchableOpacity>
      </View>

      {!created ? <>
        <Text style={styles.subtitle}>{t('receiptSubtitle')}</Text>
        {photo ? <View style={styles.photoCard}>
          <Image source={{ uri: photo.uri }} style={styles.photoPreview} resizeMode="contain" />
          {scanComplete && <Text style={styles.photoStatus}>✓ {t('receiptOcrComplete')} · {t('receiptReviewFields')}</Text>}
          <TouchableOpacity style={styles.secondaryButton} onPress={() => { setPhoto(null); setScanComplete(false); setCameraReady(false); setCameraError(''); }} disabled={loading}>
            <Text style={styles.secondaryText}>{t('receiptRetake')}</Text>
          </TouchableOpacity>
        </View> : permission?.granted ? <View style={styles.cameraBox}>
          <>
              <CameraView
                ref={cameraRef}
                style={styles.cameraPreview}
                facing="back"
                active={isFocused}
                onCameraReady={() => { setCameraReady(true); setCameraError(''); }}
                onMountError={(event) => { setCameraError(event.message || t('receiptCameraUnavailable')); setCameraReady(false); }}
              />
              {!cameraReady && <View style={styles.cameraMessage} pointerEvents="none">
                <Text style={styles.cameraMessageText}>{cameraError || t('receiptCameraStarting')}</Text>
              </View>}
          </>
        </View> : <TouchableOpacity style={styles.permissionButton} onPress={requestPermission}>
          <Text style={styles.primaryText}>{t('enableCamera')}</Text>
        </TouchableOpacity>}
        {!photo && <TouchableOpacity style={[styles.primaryButton, loading && styles.disabled]} onPress={capturePhoto} disabled={!permission?.granted || loading}>
          <Text style={styles.primaryText}>{t('receiptCapture')}</Text>
        </TouchableOpacity>}
        {photo && !scanComplete && <View style={styles.photoActions}>
          <TouchableOpacity style={[styles.primaryButton, styles.scanButton, loading && styles.disabled]} onPress={scanPhoto} disabled={loading}>
            {loading ? <ActivityIndicator color="#fff" /> : <Text style={styles.primaryText}>{t('receiptScan')}</Text>}
          </TouchableOpacity>
        </View>}
        {loading && <Text style={styles.loadingText}>{t('receiptScanning')}</Text>}

        <View style={styles.card}>
          <Text style={styles.sectionTitle}>{t('receiptDetails')}</Text>
          <Text style={styles.label}>{t('chemical')}</Text>
          <ScrollView
            style={styles.chemicalList}
            contentContainerStyle={styles.chemicalListContent}
            nestedScrollEnabled
            keyboardShouldPersistTaps="handled"
          >
            {chemicals.map((chemical) => <TouchableOpacity
              key={chemical.id}
              style={[styles.chemicalOption, String(chemical.id) === chemicalId && styles.chemicalSelected]}
              onPress={() => setChemicalId(String(chemical.id))}
            ><Text style={[styles.chemicalText, String(chemical.id) === chemicalId && styles.selectedText]}>{chemical.name} · {chemical.chemicalCode}</Text></TouchableOpacity>)}
          </ScrollView>
          <Text style={styles.label}>{t('receiptBatchNo')}</Text>
          <TextInput style={styles.input} value={batchNo} onChangeText={setBatchNo} autoCapitalize="characters" placeholder={t('batchNumberExample')} />
          <Text style={styles.label}>{t('receiptSupplier')}</Text>
          <TextInput style={styles.input} value={supplier} onChangeText={setSupplier} placeholder={t('receiptSupplierOptional')} />
          <Text style={styles.label}>{t('expiration')}</Text>
          <TextInput style={styles.input} value={expirationDate} onChangeText={setExpirationDate} placeholder="YYYY-MM-DD" />
          <Text style={styles.label}>{t('receiptQuantity')}</Text>
          <View style={styles.quantityRow}>
            <TouchableOpacity style={styles.stepButton} onPress={() => setQuantity((value) => Math.max(1, value - 1))}><Text style={styles.stepText}>−</Text></TouchableOpacity>
            <Text style={styles.quantity}>{quantity}</Text>
            <TouchableOpacity style={styles.stepButton} onPress={() => setQuantity((value) => value + 1)}><Text style={styles.stepText}>+</Text></TouchableOpacity>
          </View>
          <TouchableOpacity style={[styles.primaryButton, loading && styles.disabled]} onPress={submitReceipt} disabled={loading}>
            <Text style={styles.primaryText}>{t('receiptCreateAndLabel')}</Text>
          </TouchableOpacity>
        </View>
      </> : <View style={styles.createdCard}>
        <Text style={styles.successTitle}>{t('receiptCreated')}</Text>
        <Text style={styles.createdMeta}>{created.batch.chemicalName} · {created.batch.batchNo}</Text>
        <Image source={{ uri: created.previewUrl }} resizeMode="contain" style={styles.labelImage} />
        <Text style={styles.barcodeText}>BAT-{created.batch.batchNo}</Text>
        <TouchableOpacity style={styles.primaryButton} onPress={resetReceipt}><Text style={styles.primaryText}>{t('receiptNew')}</Text></TouchableOpacity>
      </View>}
    </ScrollView>
  </SafeAreaView>;
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#f8fafc' },
  content: { padding: 20, gap: 14 },
  header: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  title: { color: '#111827', fontSize: 26, fontWeight: '800' },
  back: { color: '#2563eb', fontSize: 16, fontWeight: '700' },
  subtitle: { color: '#64748b', fontSize: 15, lineHeight: 22 },
  cameraBox: { height: 300, overflow: 'hidden', borderRadius: 18, backgroundColor: '#0f172a' },
  cameraPreview: { width: '100%', height: '100%' },
  cameraMessage: { ...StyleSheet.absoluteFillObject, alignItems: 'center', justifyContent: 'center', padding: 20 },
  cameraMessageText: { color: '#fff', fontSize: 15, fontWeight: '700', textAlign: 'center' },
  photoCard: { gap: 10, padding: 10, borderRadius: 18, backgroundColor: '#fff', borderWidth: 1, borderColor: '#dbeafe' },
  photoPreview: { width: '100%', height: 190, backgroundColor: '#0f172a', borderRadius: 12 },
  photoStatus: { color: '#166534', backgroundColor: '#f0fdf4', padding: 12, borderRadius: 10, fontWeight: '700' },
  photoActions: { flexDirection: 'row', gap: 10 },
  secondaryButton: { minHeight: 54, flex: 1, padding: 15, borderRadius: 14, borderWidth: 1, borderColor: '#2563eb', alignItems: 'center', justifyContent: 'center', marginTop: 10 },
  secondaryText: { color: '#2563eb', fontSize: 15, fontWeight: '800', textAlign: 'center' },
  scanButton: { flex: 1 },
  permissionButton: { padding: 18, borderRadius: 14, backgroundColor: '#2563eb', alignItems: 'center' },
  primaryButton: { minHeight: 54, paddingHorizontal: 18, paddingVertical: 15, borderRadius: 14, backgroundColor: '#2563eb', alignItems: 'center', justifyContent: 'center', marginTop: 10 },
  primaryText: { color: '#fff', fontSize: 16, fontWeight: '800', textAlign: 'center' },
  loadingText: { color: '#2563eb', textAlign: 'center', fontWeight: '700' },
  card: { padding: 18, borderRadius: 18, backgroundColor: '#fff', gap: 8, elevation: 2 },
  sectionTitle: { color: '#111827', fontWeight: '800', fontSize: 19, marginBottom: 4 },
  label: { color: '#475569', fontSize: 14, fontWeight: '700', marginTop: 6 },
  chemicalList: { height: 170, flexGrow: 0, flexShrink: 0, borderRadius: 12 },
  chemicalListContent: { gap: 6, paddingVertical: 2 },
  chemicalOption: { padding: 11, borderWidth: 1, borderColor: '#cbd5e1', borderRadius: 10 },
  chemicalSelected: { borderColor: '#2563eb', backgroundColor: '#eff6ff' },
  chemicalText: { color: '#334155' },
  selectedText: { color: '#1d4ed8', fontWeight: '700' },
  input: { borderWidth: 1, borderColor: '#cbd5e1', borderRadius: 10, paddingHorizontal: 13, paddingVertical: 12, color: '#111827', fontSize: 16 },
  quantityRow: { flexDirection: 'row', alignItems: 'center', gap: 20 },
  stepButton: { width: 42, height: 42, borderRadius: 12, backgroundColor: '#e0e7ff', alignItems: 'center', justifyContent: 'center' },
  stepText: { color: '#1d4ed8', fontSize: 24, fontWeight: '800' },
  quantity: { minWidth: 35, textAlign: 'center', fontSize: 20, fontWeight: '800', color: '#111827' },
  disabled: { opacity: 0.6 },
  createdCard: { backgroundColor: '#fff', borderRadius: 20, padding: 20, alignItems: 'center', elevation: 3 },
  successTitle: { color: '#15803d', fontSize: 23, fontWeight: '800', textAlign: 'center' },
  createdMeta: { color: '#334155', fontSize: 17, marginTop: 10, textAlign: 'center' },
  labelImage: { width: '100%', height: 330, marginTop: 18, backgroundColor: '#fff' },
  barcodeText: { color: '#0f172a', fontSize: 17, fontWeight: '700', letterSpacing: 1, marginBottom: 10 },
});
