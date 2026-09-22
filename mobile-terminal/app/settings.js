import React, { useState, useEffect } from 'react';
import { StyleSheet, Text, View, TextInput, TouchableOpacity, Alert, SafeAreaView } from 'react-native';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { useRouter } from 'expo-router';
import { testConnection } from '../src/services/api';
import { useLanguage } from '../src/context/LanguageContext';

export default function SettingsScreen() {
  const router = useRouter();
  const { t } = useLanguage();
  const [ip, setIp] = useState('');
  const [testing, setTesting] = useState(false);

  useEffect(() => {
    const loadIp = async () => {
      const savedIp = await AsyncStorage.getItem('backend_ip');
      if (savedIp) {
        setIp(savedIp);
      }
    };
    loadIp();
  }, []);

  const handleSave = async () => {
    if (!ip.trim()) {
      Alert.alert(t('invalidAddress'), t('invalidAddressMessage'));
      return;
    }
    try {
      setTesting(true);
      const normalizedUrl = await testConnection(ip);
      await AsyncStorage.setItem('backend_ip', normalizedUrl);
      Alert.alert(t('success'), t('connectionSuccess'), [
        { text: 'OK', onPress: () => router.back() }
      ]);
    } catch (e) {
      Alert.alert(t('connectionFailed'), e.message || t('connectionFailed'));
    } finally {
      setTesting(false);
    }
  };

  return (
    <SafeAreaView style={styles.container}>
      <View style={styles.content}>
        <Text style={styles.title}>{t('settingsTitle')}</Text>
        <Text style={styles.subtitle}>{t('settingsSubtitle')}</Text>
        
        <Text style={styles.label}>{t('backendAddress')}</Text>
        <TextInput
          style={styles.input}
          placeholder="Örn. 192.168.1.25:5059"
          value={ip}
          onChangeText={setIp}
          autoCapitalize="none"
          autoCorrect={false}
        />

        <TouchableOpacity style={[styles.saveButton, testing && { opacity: 0.6 }]} onPress={handleSave} disabled={testing}>
          <Text style={styles.saveButtonText}>{testing ? t('testingConnection') : t('testSaveConnection')}</Text>
        </TouchableOpacity>

        <TouchableOpacity style={styles.backButton} onPress={() => router.back()}>
          <Text style={styles.backButtonText}>{t('cancel')}</Text>
        </TouchableOpacity>
      </View>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f8fafc',
  },
  content: {
    flex: 1,
    padding: 24,
    justifyContent: 'center',
  },
  title: {
    fontSize: 22,
    fontWeight: '700',
    color: '#1f2937',
    marginBottom: 8,
    textAlign: 'center',
  },
  subtitle: {
    fontSize: 14,
    color: '#6b7280',
    marginBottom: 32,
    textAlign: 'center',
    lineHeight: 20,
  },
  label: {
    fontSize: 12,
    fontWeight: '600',
    color: '#4b5563',
    marginBottom: 8,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  input: {
    backgroundColor: '#ffffff',
    borderWidth: 1,
    borderColor: '#e5e7eb',
    borderRadius: 8,
    padding: 14,
    fontSize: 16,
    color: '#1f2937',
    marginBottom: 24,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.05,
    shadowRadius: 2,
    elevation: 1,
  },
  saveButton: {
    backgroundColor: '#2563eb',
    borderRadius: 8,
    padding: 16,
    alignItems: 'center',
    marginBottom: 12,
  },
  saveButtonText: {
    color: '#ffffff',
    fontSize: 15,
    fontWeight: '600',
    letterSpacing: 0.5,
  },
  backButton: {
    padding: 12,
    alignItems: 'center',
  },
  backButtonText: {
    color: '#6b7280',
    fontSize: 15,
    fontWeight: '500',
  },
});
