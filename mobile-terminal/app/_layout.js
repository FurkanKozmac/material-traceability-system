import { useEffect } from 'react';
import { Stack, usePathname, useRouter } from 'expo-router';
import { hasSession } from '../src/services/api';
import { LanguageProvider } from '../src/context/LanguageContext';

export default function RootLayout() {
  const router = useRouter();
  const pathname = usePathname();

  useEffect(() => {
    if (pathname === '/' || pathname === '/settings') return;
    hasSession().then((active) => { if (!active) router.replace('/'); });
  }, [pathname, router]);

  return (
    <LanguageProvider>
      <Stack
        screenOptions={{
          headerShown: false,
          contentStyle: { backgroundColor: '#f8fafc' },
        }}
      />
    </LanguageProvider>
  );
}
