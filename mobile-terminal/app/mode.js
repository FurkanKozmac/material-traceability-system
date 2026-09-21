import React, { useEffect, useState } from 'react';
import { SafeAreaView, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { useRouter } from 'expo-router';
import { logout } from '../src/services/api';

export default function ModeSelectionScreen() {
  const router = useRouter();
  const [operator, setOperator] = useState('Operatör');

  useEffect(() => { AsyncStorage.getItem('operator_name').then((value) => setOperator(value || 'Operatör')); }, []);

  const endSession = async () => {
    await AsyncStorage.removeItem('operator_name');
    await logout();
    router.replace('/');
  };

  return <SafeAreaView style={styles.container}>
    <View style={styles.content}>
      <Text style={styles.session}>Aktif Operatör</Text>
      <Text style={styles.operator}>{operator}</Text>
      <Text style={styles.title}>Malzeme Tüketimi</Text>
      <Text style={styles.subtitle}>QR etiketi okutun veya barkodu elle girerek varil detayını görüntüleyin.</Text>
      <TouchableOpacity style={styles.scanButton} onPress={() => router.push('/scanner')}>
        <Text style={styles.icon}>▣</Text>
        <Text style={styles.scanText}>BARKOD OKUT / TÜKET</Text>
      </TouchableOpacity>
      <TouchableOpacity style={styles.relocationButton} onPress={() => router.push('/relocation')}>
        <Text style={styles.icon}>⇄</Text>
        <Text style={styles.scanText}>RAF TAŞIMA GÖREVLERİ</Text>
      </TouchableOpacity>
      <TouchableOpacity style={styles.settingsButton} onPress={() => router.push('/settings')}><Text style={styles.settingsText}>Sunucu Ayarları</Text></TouchableOpacity>
      <TouchableOpacity style={styles.logoutButton} onPress={endSession}><Text style={styles.logoutText}>Oturumu Kapat</Text></TouchableOpacity>
    </View>
  </SafeAreaView>;
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#f8fafc' },
  content: { flex: 1, padding: 28, justifyContent: 'center' },
  session: { color: '#6b7280', fontSize: 13, textTransform: 'uppercase' },
  operator: { color: '#111827', fontSize: 20, fontWeight: '700', marginBottom: 44 },
  title: { color: '#111827', fontSize: 28, fontWeight: '800', textAlign: 'center' },
  subtitle: { color: '#6b7280', fontSize: 16, lineHeight: 23, textAlign: 'center', marginTop: 10, marginBottom: 32 },
  scanButton: { backgroundColor: '#16a34a', borderRadius: 18, paddingVertical: 30, alignItems: 'center' },
  relocationButton: { backgroundColor: '#2563eb', borderRadius: 18, paddingVertical: 22, alignItems: 'center', marginTop: 14 },
  icon: { color: '#fff', fontSize: 42, marginBottom: 8 },
  scanText: { color: '#fff', fontSize: 18, fontWeight: '800' },
  settingsButton: { padding: 16, alignItems: 'center', marginTop: 16 },
  settingsText: { color: '#2563eb', fontWeight: '600' },
  logoutButton: { padding: 16, alignItems: 'center' },
  logoutText: { color: '#dc2626', fontWeight: '600' },
});
