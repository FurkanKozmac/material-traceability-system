import React, { useEffect, useState } from 'react';
import { ActivityIndicator, Alert, SafeAreaView, ScrollView, StyleSheet, Text, TextInput, TouchableOpacity, View, Vibration } from 'react-native';
import { CameraView, useCameraPermissions } from 'expo-camera';
import { useRouter } from 'expo-router';
import { completeRelocationTask, listRelocationTasks } from '../src/services/api';
import { useLanguage } from '../src/context/LanguageContext';

export default function RelocationScreen() {
  const router = useRouter();
  const { t } = useLanguage();
  const [permission, requestPermission] = useCameraPermissions();
  const [tasks, setTasks] = useState([]);
  const [task, setTask] = useState(null);
  const [step, setStep] = useState('unit');
  const [manual, setManual] = useState('');
  const [loading, setLoading] = useState(true);
  const [scanned, setScanned] = useState(false);

  const load = async () => {
    try { setTasks((await listRelocationTasks()).filter((item) => item.status === 'Pending')); }
    catch (error) { Alert.alert(t('moveFailed'), error.message); }
    finally { setLoading(false); }
  };
  useEffect(() => { load(); }, []);

  const processCode = async (raw) => {
    if (!task || loading) return;
    const code = raw.trim();
    if (step === 'unit') {
      if (code.toUpperCase() !== task.unitBarcode.toUpperCase()) {
        Alert.alert(t('wrongUnit'), `${t('expectedUnit')}: ${task.unitBarcode}`); setScanned(false); return;
      }
      Vibration.vibrate(100); setStep('address'); setManual(''); setScanned(false); return;
    }
    if (code.toUpperCase() !== `ADR-${task.toAddressCode}`.toUpperCase()) {
      Alert.alert(t('wrongRack'), `${t('expectedRack')}: ADR-${task.toAddressCode}`); setScanned(false); return;
    }
    setLoading(true);
    try {
      await completeRelocationTask(task.id, task.unitBarcode, code);
      Vibration.vibrate([0, 100, 50, 100]);
      Alert.alert(t('success'), t('movedSuccess'));
      setTask(null); setStep('unit'); setManual(''); setScanned(false); await load();
    } catch (error) { Alert.alert(t('moveFailed'), error.message); setScanned(false); }
    finally { setLoading(false); }
  };

  return <SafeAreaView style={styles.container}><ScrollView contentContainerStyle={styles.content}>
    <View style={styles.header}><Text style={styles.title}>{t('relocation')}</Text><TouchableOpacity onPress={() => router.back()}><Text style={styles.back}>{t('back')}</Text></TouchableOpacity></View>
    {!task ? <><Text style={styles.help}>{t('selectTaskHelp')}</Text>
      {loading && <ActivityIndicator color="#2563eb" />}
      {tasks.map((item) => <TouchableOpacity key={item.id} style={styles.task} onPress={() => { setTask(item); setStep('unit'); setScanned(false); }}>
        <Text style={styles.taskTitle}>Görev #{item.id} · {item.unitBarcode}</Text><Text style={styles.route}>{item.fromAddressCode}  →  {item.toAddressCode}</Text>
      </TouchableOpacity>)}
      {!loading && !tasks.length && <Text style={styles.empty}>{t('noPendingTasks')}</Text>}
    </> : <>
      <View style={styles.active}><Text style={styles.eyebrow}>{t('activeTask')} #{task.id}</Text><Text style={styles.taskTitle}>{task.unitBarcode}</Text><Text style={styles.route}>{task.fromAddressCode} → {task.toAddressCode}</Text><TouchableOpacity onPress={() => setTask(null)}><Text style={styles.change}>{t('changeTask')}</Text></TouchableOpacity></View>
      <Text style={styles.step}>{step === 'unit' ? `1/2 · ${t('stepUnit')}: ${task.unitBarcode}` : `2/2 · ${t('stepRack')}: ADR-${task.toAddressCode}`}</Text>
      <View style={styles.inputRow}><TextInput style={styles.input} autoCapitalize="characters" value={manual} onChangeText={setManual} placeholder={step === 'unit' ? task.unitBarcode : `ADR-${task.toAddressCode}`} /><TouchableOpacity style={styles.submit} onPress={() => processCode(manual)}><Text style={styles.submitText}>{t('verify')}</Text></TouchableOpacity></View>
      {permission?.granted ? <View style={styles.camera}><CameraView style={StyleSheet.absoluteFillObject} barcodeScannerSettings={{ barcodeTypes: ['qr'] }} onBarcodeScanned={scanned ? undefined : ({ data }) => { setScanned(true); processCode(data); }} /><View style={styles.reticle} /></View> : <TouchableOpacity style={styles.submit} onPress={requestPermission}><Text style={styles.submitText}>{t('enableCamera')}</Text></TouchableOpacity>}
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
