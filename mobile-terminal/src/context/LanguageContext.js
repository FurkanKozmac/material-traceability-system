import React, { createContext, useContext, useState, useEffect } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';

const translations = {
  tr: {
    title: 'Operatör Doğrulama',
    subtitle: 'Üretim ve depo işlemlerine erişmek için kullanıcı bilgilerinizle giriş yapın.',
    operatorLabel: 'Operatör Kullanıcı Adı / Sicil No',
    operatorPlaceholder: 'Örn. OP-1234 veya kullanici_adi',
    passwordLabel: 'Parola',
    passwordPlaceholder: 'Parolanızı giriniz',
    loginButton: 'OTURUMU BAŞLAT',
    connecting: 'BAĞLANILIYOR…',
    missingInfo: 'Eksik Bilgi',
    missingInfoMessage: 'Lütfen operatör kullanıcı adını ve parolayı giriniz.',
    loginFailed: 'Giriş Başarısız',
    serverSettings: 'Sunucu Bağlantı Ayarları',
  },
  en: {
    title: 'Operator Authentication',
    subtitle: 'Log in with your credentials to access manufacturing and warehouse operations.',
    operatorLabel: 'Operator Username / Badge ID',
    operatorPlaceholder: 'e.g. OP-1234 or username',
    passwordLabel: 'Password',
    passwordPlaceholder: 'Enter your password',
    loginButton: 'START SESSION',
    connecting: 'CONNECTING…',
    missingInfo: 'Missing Information',
    missingInfoMessage: 'Please enter operator username and password.',
    loginFailed: 'Login Failed',
    serverSettings: 'Server Connection Settings',
  }
};

const LanguageContext = createContext();

export function LanguageProvider({ children }) {
  const [language, setLanguageState] = useState('tr');

  useEffect(() => {
    AsyncStorage.getItem('app_lang').then((saved) => {
      if (saved) setLanguageState(saved);
    });
  }, []);

  const setLanguage = async (lang) => {
    await AsyncStorage.setItem('app_lang', lang);
    setLanguageState(lang);
  };

  const t = (key) => translations[language]?.[key] || translations['tr']?.[key] || key;

  return (
    <LanguageContext.Provider value={{ language, setLanguage, t }}>
      {children}
    </LanguageContext.Provider>
  );
}

export function useLanguage() {
  return useContext(LanguageContext);
}
