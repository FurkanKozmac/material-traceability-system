import React, { createContext, useContext, useState } from 'react';

const translations = {
  tr: {
    welcome: 'Hoş Geldiniz',
    systemTitle: 'Malzeme İzlenebilirlik Sistemi (MTS)',
    username: 'Kullanıcı Adı',
    usernamePlaceholder: 'Örn. kullanici_adi',
    password: 'Parola',
    login: 'Sisteme Giriş Yap',
    loggingIn: 'Giriş Yapılıyor...',
    loginFailed: 'Kullanıcı adı veya parola hatalı. Lütfen yeniden deneyin.',
    logout: 'Çıkış Yap',
    system: 'Sistem',
    overview: 'Genel Bakış',
    chemicals: 'Kimyasal Kataloğu',
    batches: 'Hammadde Partileri',
    addresses: 'Depo Rafları',
    units: 'Stok Birimleri',
    qrLabels: 'QR Barkod Etiketleri',
    relocationTasks: 'Raf Taşıma Görevleri',
    language: 'Dil / Language',

    // Dashboard
    dashboardTitle: 'Sistem Genel Görünümü',
    dashboardDesc: 'Fabrika genelindeki kimyasal malzeme hareketlerini anlık olarak izleyin ve yönetin.',
    inStockDrums: 'Stoktaki Variller',
    totalConsumed: 'Toplam Tüketilen',
    pendingOps: 'Bekleyen İşlemler',
    expiredBlocked: 'Süresi Dolan / Bloke',
    recentConsumptionLogs: 'Son Malzeme Tüketim Kayıtları',
    time: 'Zaman',
    action: 'İşlem',
    barcode: 'Barkod',
    chemical: 'Kimyasal',
    operator: 'Operatör',
    noTransactionsYet: 'Henüz işlem bulunmuyor',
    noTransactionsDesc: 'Operatörler kimyasal varilleri okuttukça tüketim kayıtları burada görüntülenecektir.',
    consumed: 'TÜKETİLDİ',
    unknown: 'Bilinmiyor',

    // QR Label Manager
    qrLabelTitle: 'QR Barkod Etiketleri',
    qrLabelSubtitle: 'Önce parti etiketini, ardından partiye ait varil etiketlerini yazdırın.',
    rawMaterialBatch: 'Hammadde Partileri',
    selectBatchPlaceholder: 'Parti numarası veya kimyasal adıyla arayın…',
    noBatchSelected: 'Henüz bir parti seçilmedi',
    noBatchSelectedDesc: 'Etiketleri görüntülemek ve yazdırmak için lütfen yukarıdan bir hammadde partisi seçiniz.',
    selectAll: 'Tümünü Seç',
    selectAvailableOnly: 'Sadece Müsaitleri Seç',
    selectedCount: 'Seçilen',
    labelsCount: 'etiket',
    printSelected: 'Etiketi Yazdır',
    batchOpeningLabel: 'Parti açılış etiketi',
    batchOpeningLabelDesc: 'Mobil terminalde önce bu etiket okutulur; ardından yalnızca bu partiye ait variller işleme alınabilir.',
    printBatchLabel: 'Batch Etiketini Yazdır',
    available: 'Müsait',
    expired: 'Süresi Dolmuş',
    depleted: 'Tüketildi',
    expDate: 'SKT',

    // Relocation Tasks
    relocationTitle: 'Raf Taşıma Görevleri',
    relocationSubtitle: 'Görevi planlayın; fiziksel taşımayı mobil terminalde çift QR doğrulamasıyla tamamlayın.',
    newTask: 'Yeni Görev',
    stockUnitBarcode: 'Stok Birimi Barkodu',
    findUnit: 'Birimi Bul',
    compatibleTargetShelf: 'Uyumlu Hedef Raf',
    createTask: 'Görev Oluştur',
    cancelTask: 'İptal Et',
    taskPending: 'BEKLİYOR',
    taskCompleted: 'TAMAMLANDI',
    taskCancelled: 'İPTAL EDİLDİ',
    source: 'Kaynak',
    target: 'Hedef',
    noTasksInStatus: 'Bu durumda görev bulunmuyor.',
    completeOnMobile: 'Mobilde tamamlanır',
  },
  en: {
    welcome: 'Welcome',
    systemTitle: 'Material Traceability System (MTS)',
    username: 'Username',
    usernamePlaceholder: 'e.g. username',
    password: 'Password',
    login: 'Log In',
    loggingIn: 'Logging in...',
    loginFailed: 'Invalid username or password. Please try again.',
    logout: 'Log Out',
    system: 'System',
    overview: 'Overview',
    chemicals: 'Chemical Catalog',
    batches: 'Raw Material Batches',
    addresses: 'Storage Racks',
    units: 'Stock Units',
    qrLabels: 'QR Barcode Labels',
    relocationTasks: 'Relocation Tasks',
    language: 'Language',

    // Dashboard
    dashboardTitle: 'System Overview',
    dashboardDesc: 'Monitor and manage chemical material movements across the plant in real time.',
    inStockDrums: 'In-Stock Drums',
    totalConsumed: 'Total Consumed',
    pendingOps: 'Pending Operations',
    expiredBlocked: 'Expired / Blocked',
    recentConsumptionLogs: 'Recent Material Consumption Logs',
    time: 'Time',
    action: 'Action',
    barcode: 'Barcode',
    chemical: 'Chemical',
    operator: 'Operator',
    noTransactionsYet: 'No transactions yet',
    noTransactionsDesc: 'Consumption logs will appear here as operators scan chemical drums.',
    consumed: 'CONSUMED',
    unknown: 'Unknown',

    // QR Label Manager
    qrLabelTitle: 'QR Barcode Labels',
    qrLabelSubtitle: 'First print the batch label, then print the individual drum labels for that batch.',
    rawMaterialBatch: 'Raw Material Batches',
    selectBatchPlaceholder: 'Search by batch number or chemical name…',
    noBatchSelected: 'No batch selected yet',
    noBatchSelectedDesc: 'Please select a raw material batch above to view and print labels.',
    selectAll: 'Select All',
    selectAvailableOnly: 'Select Available Only',
    selectedCount: 'Selected',
    labelsCount: 'labels',
    printSelected: 'Print Labels',
    batchOpeningLabel: 'Batch Opening Label',
    batchOpeningLabelDesc: 'Scan this label first on the mobile terminal; only drums from this batch will be accepted for processing.',
    printBatchLabel: 'Print Batch Label',
    available: 'Available',
    expired: 'Expired',
    depleted: 'Depleted',
    expDate: 'Exp Date',

    // Relocation Tasks
    relocationTitle: 'Relocation Tasks',
    relocationSubtitle: 'Plan relocation tasks; complete physical transfers on the mobile terminal via dual-QR verification.',
    newTask: 'New Task',
    stockUnitBarcode: 'Stock Unit Barcode',
    findUnit: 'Find Unit',
    compatibleTargetShelf: 'Compatible Target Rack',
    createTask: 'Create Task',
    cancelTask: 'Cancel Task',
    taskPending: 'PENDING',
    taskCompleted: 'COMPLETED',
    taskCancelled: 'CANCELLED',
    source: 'Source',
    target: 'Target',
    noTasksInStatus: 'No tasks found in this status.',
    completeOnMobile: 'Completed on mobile',
  }
};

const LanguageContext = createContext();

export function LanguageProvider({ children }) {
  const [language, setLanguageState] = useState(() => localStorage.getItem('app_lang') || 'tr');

  const setLanguage = (lang) => {
    localStorage.setItem('app_lang', lang);
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
