# Çalışma kuralları

- Önce ilgili dosyaları oku, sonra değiştir. Tahmin yürütme, emin olmadığın yeri sor.
- Sadece istenen kapsamı değiştir. İlgisiz refactor yapma.
- Mevcut kod stiline uy (DTO'lar record, servis arayüzleri Services/Interfaces,
  implementasyonlar Services/Implementations).
- Gerçek sır, parola, anahtar yazma. Parola/hash hiçbir response ve log'da görünmesin.
- Her değişiklikten sonra `dotnet build backend/backend.sln` ve
  `dotnet test backend.Tests/backend.Tests.csproj --filter "Category!=Testcontainers"` çalıştır.
- İş bitince: neyi neden değiştirdiğini, hangi dosyalara dokunduğunu ve bilerek
  yapmadığın şeyleri kısaca özetle.
- Prompt açıkça izin vermedikçe commit atma. `git push` yalnızca prompt açıkça izin verirse; force push, rebase, reset asla.

# Teslim kuralı (inceleme paketi)

Görev bitince, commit atmadan, `.review/<görev-adı>.md` dosyasını şu komutlarla üret
(<görev-adı> promptta verilir):

mkdir -p .review
{
  echo "## git status"; git status --short
  echo; echo "## diff stat"; git diff --stat
  echo; echo "## diff"; git diff
  for f in $(git ls-files --others --exclude-standard | grep -v -e '^\.review/' -e '^AGENTS\.md$' -e '^REPO_REPORT\.md$'); do
    echo; echo "## NEW FILE: $f"; cat "$f"
  done
  echo; echo "## test sonucu (özet)"; <son çalıştırılan test komutunun son 25 satırı>
} > .review/<görev-adı>.md

- Dosyaya .env ve gizli değer girmediğinden emin ol (maskele).
- Dosya 1500 satırı aşarsa görevi bölmem için bana haber ver.
- Sohbette sadece 5 satırlık özet ver: ne değişti, ne bilerek yapılmadı, bir belirsizlik var mı.

# Güvenlik ve gizli bilgi
- `.env` ve `.env.test` okunmaz, yazdırılmaz, düzenlenmez. Docker testinde `--env-file` ile geçici dosya kullan, iş bitince sil.
- Çalıştıramadığın komutu "çalıştırdım" diye yazma; gerçek çıktıyı göster, çalışmadıysa açıkça söyle.
- Dışarıya yalnızca Caddy 80/443 yayınlar. Başka serviste `ports:` ekleme.
- Yeni endpoint'te rol açık yazılır. Silme/ayar/yönetim yalnızca Admin; Operator için 403 testi ekle.
- Production'da zayıf veya eksik secret ile başlamama (fail-fast) korunur. `demo` kullanıcısı Operator kalır.
- Çok dosyalı veya mimariyi etkileyen işte önce 5-8 satırlık plan + dosya listesi ver, onay bekle.

# Token disiplini
- `bin/`, `obj/`, `node_modules/`, `dist/`, `backups/`, OCR model dosyaları okunmaz.
- Çıktıyı kısalt (`| tail -20`); önce `git diff --stat`, gerekirse tam diff. Büyük dosyada ilgili aralığı oku.
