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
    activeOperator: 'Aktif Operatör',
    materialConsumption: 'Malzeme Tüketimi',
    scanMaterialHelp: 'QR etiketi okutun veya barkodu elle girerek varil detayını görüntüleyin.',
    scanConsume: 'BARKOD OKUT / TÜKET',
    relocationTasks: 'RAF TAŞIMA GÖREVLERİ',
    serverSettingsShort: 'Sunucu Ayarları',
    logout: 'Oturumu Kapat',
    barrelScan: 'Varil Barkod İşlemi',
    back: 'Geri',
    query: 'Sorgula',
    invalidBarcode: 'Geçersiz Barkod',
    invalidBarcodeMessage: 'Geçersiz MTS Barkodu! Lütfen batch veya varil etiketi okutunuz.',
    scanBatchFirst: 'Önce Batch Okutulmalı',
    scanBatchFirstMessage: 'Varil tüketimine başlamadan önce batch QR etiketini okutunuz.',
    wrongBatch: 'Yanlış Hammadde Partisi',
    barcodeNotFound: 'Barkod bulunamadı',
    barrelLookupFailed: 'Varil bilgisi alınamadı.',
    change: 'Değiştir',
    activeBatch: 'AKTİF HAMMADDE PARTİSİ',
    rack: 'Raf',
    unassignedRack: 'Rafa atanmamış',
    total: 'Toplam',
    available: 'Müsait',
    consumed: 'Tüketilen',
    continueUnitScan: 'BİRİM TARAMAYA GEÇ',
    enableCamera: 'Kamerayı Etkinleştir',
    chemicalCode: 'Kimyasal Kodu',
    batch: 'Parti',
    barcode: 'Barkod',
    expiration: 'SKT',
    status: 'Durum',
    depleted: 'TÜKETİLMİŞ',
    consume: 'TÜKETİME VER (CONSUME)',
    success: 'Başarılı',
    consumeSuccess: 'Varil başarıyla tüketime verildi!',
    consumeFailed: 'Tüketim başarısız',
    sameBatchScan: 'Aynı Partiden Yeni Birim Tara',
    relocation: 'Raf Taşıma',
    selectTaskHelp: 'Bir görev seçin. Önce stok birimini, ardından hedef raf QR etiketini okutun.',
    task: 'Görev',
    wrongUnit: 'Yanlış Stok Birimi',
    expectedUnit: 'Bu görev için okutulması gereken birim',
    wrongRack: 'Yanlış Hedef Raf',
    expectedRack: 'Beklenen raf etiketi',
    movedSuccess: 'Stok birimi hedef rafa taşındı.',
    moveFailed: 'Taşıma Başarısız',
    noPendingTasks: 'Bekleyen taşıma görevi yok.',
    activeTask: 'AKTİF GÖREV',
    changeTask: 'Görevi Değiştir',
    stepUnit: 'Stok birimini okutun',
    stepRack: 'Hedef rafı okutun',
    verify: 'Doğrula',
    settingsTitle: 'Sunucu Bağlantı Ayarları',
    settingsSubtitle: 'Yerel ağdaki .NET Backend API adresini belirtiniz.',
    backendAddress: 'Backend Sunucu IP / URL',
    invalidAddress: 'Geçersiz Adres',
    invalidAddressMessage: 'Lütfen geçerli bir IP adresi giriniz.',
    testingConnection: 'BAĞLANTI SINANIYOR…',
    testSaveConnection: 'BAĞLANTIYI TEST ET VE KAYDET',
    connectionSuccess: 'Bağlantı doğrulandı ve sunucu adresi kaydedildi.',
    connectionFailed: 'Bağlantı Başarısız',
    cancel: 'İptal',
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
    activeOperator: 'Active Operator',
    materialConsumption: 'Material Consumption',
    scanMaterialHelp: 'Scan a QR label or enter a barcode manually to view barrel details.',
    scanConsume: 'SCAN BARCODE / CONSUME',
    relocationTasks: 'RELOCATION TASKS',
    serverSettingsShort: 'Server Settings',
    logout: 'Log Out',
    barrelScan: 'Barrel Barcode Operation',
    back: 'Back',
    query: 'Query',
    invalidBarcode: 'Invalid Barcode',
    invalidBarcodeMessage: 'Invalid MTS barcode. Scan a batch or barrel label.',
    scanBatchFirst: 'Scan Batch First',
    scanBatchFirstMessage: 'Scan the batch QR label before consuming a barrel.',
    wrongBatch: 'Wrong Raw Material Batch',
    barcodeNotFound: 'Barcode Not Found',
    barrelLookupFailed: 'Barrel details could not be loaded.',
    change: 'Change',
    activeBatch: 'ACTIVE RAW MATERIAL BATCH',
    rack: 'Rack',
    unassignedRack: 'Not assigned to a rack',
    total: 'Total',
    available: 'Available',
    consumed: 'Consumed',
    continueUnitScan: 'CONTINUE TO UNIT SCAN',
    enableCamera: 'Enable Camera',
    chemicalCode: 'Chemical Code',
    batch: 'Batch',
    barcode: 'Barcode',
    expiration: 'Expiry',
    status: 'Status',
    depleted: 'CONSUMED',
    consume: 'CONSUME BARREL',
    success: 'Success',
    consumeSuccess: 'Barrel was consumed successfully!',
    consumeFailed: 'Consumption Failed',
    sameBatchScan: 'Scan Another Unit from This Batch',
    relocation: 'Relocation',
    selectTaskHelp: 'Select a task. Scan the stock unit first, then the target rack QR label.',
    task: 'Task',
    wrongUnit: 'Wrong Stock Unit',
    expectedUnit: 'The unit required for this task',
    wrongRack: 'Wrong Target Rack',
    expectedRack: 'Expected rack label',
    movedSuccess: 'Stock unit moved to the target rack.',
    moveFailed: 'Relocation Failed',
    noPendingTasks: 'No pending relocation tasks.',
    activeTask: 'ACTIVE TASK',
    changeTask: 'Change Task',
    stepUnit: 'Scan the stock unit',
    stepRack: 'Scan the target rack',
    verify: 'Verify',
    settingsTitle: 'Server Connection Settings',
    settingsSubtitle: 'Enter the .NET Backend API address on the local network.',
    backendAddress: 'Backend Server IP / URL',
    invalidAddress: 'Invalid Address',
    invalidAddressMessage: 'Enter a valid IP address.',
    testingConnection: 'TESTING CONNECTION…',
    testSaveConnection: 'TEST AND SAVE CONNECTION',
    connectionSuccess: 'Connection verified and server address saved.',
    connectionFailed: 'Connection Failed',
    cancel: 'Cancel',
  }
};

const LanguageContext = createContext();

export function LanguageProvider({ children }) {
  const [language, setLanguageState] = useState('tr');
  const [ready, setReady] = useState(false);

  useEffect(() => {
    AsyncStorage.getItem('app_lang').then((saved) => {
      if (saved) setLanguageState(saved);
    }).finally(() => setReady(true));
  }, []);

  const setLanguage = async (lang) => {
    await AsyncStorage.setItem('app_lang', lang);
    setLanguageState(lang);
  };

  const t = (key) => translations[language]?.[key] || translations['tr']?.[key] || key;

  if (!ready) return null;

  return (
    <LanguageContext.Provider value={{ language, setLanguage, t }}>
      {children}
    </LanguageContext.Provider>
  );
}

export function useLanguage() {
  return useContext(LanguageContext);
}
