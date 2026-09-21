import React, { useEffect, useState } from 'react';
import { ActivityIndicator, Alert, SafeAreaView, ScrollView, StyleSheet, Text, TextInput, TouchableOpacity, View, Vibration } from 'react-native';
import { CameraView, useCameraPermissions } from 'expo-camera';
import { useRouter } from 'expo-router';
import { completeRelocationTask, listRelocationTasks } from '../src/services/api';

export default function RelocationScreen() {
  const router = useRouter();
  const [permission, requestPermission] = useCameraPermissions();
  const [tasks, setTasks] = useState([]);
  const [task, setTask] = useState(null);
  const [step, setStep] = useState('unit');
  const [manual, setManual] = useState('');
  const [loading, setLoading] = useState(true);
  const [scanned, setScanned] = useState(false);

  const load = async () => {
    try { setTasks((await listRelocationTasks()).filter((item) => item.status === 'Pending')); }
    catch (error) { Alert.alert('Hata', error.message); }
    finally { setLoading(false); }
  };
  useEffect(() => { load(); }, []);

  const processCode = async (raw) => {
    if (!task || loading) return;
    const code = raw.trim();
    if (step === 'unit') {
      if (code.toUpperCase() !== task.unitBarcode.toUpperCase()) {
        Alert.alert('Yanlış Stok Birimi', `Bu görev için ${task.unitBarcode} okutulmalıdır.`); setScanned(false); return;
      }
      Vibration.vibrate(100); setStep('address'); setManual(''); setScanned(false); return;
    }
    if (code.toUpperCase() !== `ADR-${task.toAddressCode}`.toUpperCase()) {
      Alert.alert('Yanlış Hedef Raf', `Beklenen raf etiketi: ADR-${task.toAddressCode}`); setScanned(false); return;
    }
    setLoading(true);
    try {
      await completeRelocationTask(task.id, task.unitBarcode, code);
      Vibration.vibrate([0, 100, 50, 100]);
      Alert.alert('Başarılı', 'Stok birimi hedef rafa taşındı.');
      setTask(null); setStep('unit'); setManual(''); setScanned(false); await load();
    } catch (error) { Alert.alert('Taşıma Başarısız', error.message); setScanned(false); }
    finally { setLoading(false); }
  };

  return <SafeAreaView style={styles.container}><ScrollView contentContainerStyle={styles.content}>
    <View style={styles.header}><Text style={styles.title}>Raf Taşıma</Text><TouchableOpacity onPress={() => router.back()}><Text style={styles.back}>Geri</Text></TouchableOpacity></View>
    {!task ? <><Text style={styles.help}>Bir görev seçin. Önce stok birimini, ardından hedef raf QR etiketini okutun.</Text>
      {loading && <ActivityIndicator color="#2563eb" />}
      {tasks.map((item) => <TouchableOpacity key={item.id} style={styles.task} onPress={() => { setTask(item); setStep('unit'); setScanned(false); }}>
        <Text style={styles.taskTitle}>Görev #{item.id} · {item.unitBarcode}</Text><Text style={styles.route}>{item.fromAddressCode}  →  {item.toAddressCode}</Text>
      </TouchableOpacity>)}
      {!loading && !tasks.length && <Text style={styles.empty}>Bekleyen taşıma görevi yok.</Text>}
    </> : <>
      <View style={styles.active}><Text style={styles.eyebrow}>AKTİF GÖREV #{task.id}</Text><Text style={styles.taskTitle}>{task.unitBarcode}</Text><Text style={styles.route}>{task.fromAddressCode} → {task.toAddressCode}</Text><TouchableOpacity onPress={() => setTask(null)}><Text style={styles.change}>Görevi Değiştir</Text></TouchableOpacity></View>
      <Text style={styles.step}>{step === 'unit' ? `1/2 · Stok birimini okutun: ${task.unitBarcode}` : `2/2 · Hedef rafı okutun: ADR-${task.toAddressCode}`}</Text>
      <View style={styles.inputRow}><TextInput style={styles.input} autoCapitalize="characters" value={manual} onChangeText={setManual} placeholder={step === 'unit' ? task.unitBarcode : `ADR-${task.toAddressCode}`} /><TouchableOpacity style={styles.submit} onPress={() => processCode(manual)}><Text style={styles.submitText}>Doğrula</Text></TouchableOpacity></View>
      {permission?.granted ? <View style={styles.camera}><CameraView style={StyleSheet.absoluteFillObject} barcodeScannerSettings={{ barcodeTypes: ['qr'] }} onBarcodeScanned={scanned ? undefined : ({ data }) => { setScanned(true); processCode(data); }} /><View style={styles.reticle} /></View> : <TouchableOpacity style={styles.submit} onPress={requestPermission}><Text style={styles.submitText}>Kamerayı Etkinleştir</Text></TouchableOpacity>}
    </>}
  </ScrollView></SafeAreaView>;
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#f8fafc' }, content: { padding: 20, gap: 14 },
  header: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }, title: { fontSize: 25, fontWeight: '800' }, back: { color: '#2563eb', fontWeight: '700' },
  help: { color: '#64748b', lineHeight: 21 }, task: { backgroundColor: '#fff', borderWidth: 1, borderColor: '#dbeafe', borderRadius: 14, padding: 17 }, taskTitle: { fontWeight: '800', color: '#0f172a', fontSize: 16 }, route: { color: '#2563eb', fontWeight: '700', marginTop: 7 }, empty: { textAlign: 'center', color: '#64748b', padding: 30 },
  active: { backgroundColor: '#eff6ff', borderWidth: 1, borderColor: '#93c5fd', padding: 16, borderRadius: 14 }, eyebrow: { fontSize: 11, color: '#1d4ed8', fontWeight: '800' }, change: { color: '#dc2626', fontWeight: '700', marginTop: 10 }, step: { fontSize: 16, fontWeight: '700', color: '#334155' },
  inputRow: { flexDirection: 'row', gap: 8 }, input: { flex: 1, backgroundColor: '#fff', borderWidth: 1, borderColor: '#cbd5e1', borderRadius: 10, padding: 13 }, submit: { backgroundColor: '#2563eb', padding: 14, borderRadius: 10, alignItems: 'center', justifyContent: 'center' }, submitText: { color: '#fff', fontWeight: '800' },
  camera: { height: 300, backgroundColor: '#111827', borderRadius: 16, overflow: 'hidden', alignItems: 'center', justifyContent: 'center' }, reticle: { width: 210, height: 210, borderWidth: 3, borderColor: '#22c55e', borderRadius: 14 },
});
