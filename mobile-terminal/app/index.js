import React, { useState, useEffect } from 'react';
import { StyleSheet, Text, View, TextInput, TouchableOpacity, Alert, SafeAreaView } from 'react-native';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { useRouter } from 'expo-router';
import * as SecureStore from 'expo-secure-store';
import { login } from '../src/services/api';
import { useLanguage } from '../src/context/LanguageContext';

export default function LoginScreen() {
  const router = useRouter();
  const { language, setLanguage, t } = useLanguage();
  const [operator, setOperator] = useState('');
  const [password, setPassword] = useState('');
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    const loadOperator = async () => {
      const savedOperator = await AsyncStorage.getItem('operator_name');
      if (savedOperator) {
        setOperator(savedOperator);
      }
    };
    loadOperator();
  }, []);

  const handleLogin = async () => {
    if (!operator.trim() || !password.trim()) {
      Alert.alert(t('missingInfo'), t('missingInfoMessage'));
      return;
    }
    try {
      setSubmitting(true);
      const data = await login(operator.trim(), password.trim(), 'DB');
      await SecureStore.setItemAsync('token', data.token);
      if (data.refreshToken) {
        await SecureStore.setItemAsync('refreshToken', data.refreshToken);
      }
      await AsyncStorage.setItem('session_user', JSON.stringify({
        username: data.username,
        roles: data.roles || [],
        permissions: data.permissions || [],
        authorizedShops: data.authorizedShops || [],
      }));
      await AsyncStorage.setItem('operator_name', operator.trim());
      router.replace('/mode');
    } catch (e) {
      Alert.alert(t('loginFailed'), e.message || 'Oturum açılamadı.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <SafeAreaView style={styles.container}>
      <View style={styles.content}>
        {/* Top Language Switcher */}
        <View style={styles.langContainer}>
          <TouchableOpacity
            style={[styles.langButton, language === 'tr' && styles.langButtonActive]}
            onPress={() => setLanguage('tr')}
          >
            <Text style={[styles.langText, language === 'tr' && styles.langTextActive]}>TR</Text>
          </TouchableOpacity>
          <TouchableOpacity
            style={[styles.langButton, language === 'en' && styles.langButtonActive]}
            onPress={() => setLanguage('en')}
          >
            <Text style={[styles.langText, language === 'en' && styles.langTextActive]}>EN</Text>
          </TouchableOpacity>
        </View>

        <View style={styles.header}>
          <View style={styles.logoDot} />
          <Text style={styles.logoText}>MTS Terminal</Text>
        </View>
        
        <Text style={styles.title}>{t('title')}</Text>
        <Text style={styles.subtitle}>{t('subtitle')}</Text>

        <Text style={styles.label}>{t('operatorLabel')}</Text>
        <TextInput
          style={styles.input}
          placeholder={t('operatorPlaceholder')}
          value={operator}
          onChangeText={setOperator}
          autoCapitalize="none"
          autoCorrect={false}
        />

        <Text style={styles.label}>{t('passwordLabel')}</Text>
        <TextInput
          style={styles.input}
          placeholder={t('passwordPlaceholder')}
          value={password}
          onChangeText={setPassword}
          secureTextEntry
          autoCapitalize="none"
        />

        <TouchableOpacity style={[styles.loginButton, submitting && styles.disabled]} onPress={handleLogin} disabled={submitting}>
          <Text style={styles.loginButtonText}>{submitting ? t('connecting') : t('loginButton')}</Text>
        </TouchableOpacity>

        <TouchableOpacity 
          style={styles.settingsLink} 
          onPress={() => router.push('/settings')}
        >
          <Text style={styles.settingsLinkText}>{t('serverSettings')}</Text>
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
  langContainer: {
    flexDirection: 'row',
    justifyContent: 'flex-end',
    marginBottom: 16,
    gap: 8,
  },
  langButton: {
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 6,
    borderWidth: 1,
    borderColor: '#cbd5e1',
    backgroundColor: '#ffffff',
  },
  langButtonActive: {
    backgroundColor: '#2563eb',
    borderColor: '#2563eb',
  },
  langText: {
    fontSize: 12,
    fontWeight: '700',
    color: '#475569',
  },
  langTextActive: {
    color: '#ffffff',
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 32,
  },
  logoDot: {
    width: 12,
    height: 12,
    borderRadius: 6,
    backgroundColor: '#2563eb',
    marginRight: 8,
  },
  logoText: {
    fontSize: 18,
    fontWeight: '700',
    color: '#1f2937',
    letterSpacing: -0.5,
  },
  title: {
    fontSize: 24,
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
  loginButton: {
    backgroundColor: '#2563eb',
    borderRadius: 8,
    padding: 16,
    alignItems: 'center',
    marginBottom: 20,
  },
  disabled: { opacity: 0.6 },
  loginButtonText: {
    color: '#ffffff',
    fontSize: 15,
    fontWeight: '600',
    letterSpacing: 0.5,
  },
  settingsLink: {
    alignItems: 'center',
    padding: 10,
  },
  settingsLinkText: {
    color: '#2563eb',
    fontSize: 14,
    fontWeight: '500',
  },
});
