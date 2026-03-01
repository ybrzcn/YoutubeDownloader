export type Lang = 'tr' | 'en'

export const translations = {
  tr: {
    subtitle: 'Video veya ses indir',
    placeholder: 'https://youtu.be/...',
    fetch: 'Getir',
    error: "Video bilgisi alınamadı. URL'yi kontrol et ve tekrar dene.",
    formatLabel: 'Format Seçin',
    downloading: 'İndirme başlatılıyor...',
    downloadAudio: 'Sesi İndir (.m4a)',
    downloadVideo: (quality: string) => `Videoyu İndir — ${quality} .mp4`,
    themeLight: 'Açık temaya geç',
    themeDark: 'Koyu temaya geç',
    langToggle: 'Switch to English',
  },
  en: {
    subtitle: 'Download video or audio',
    placeholder: 'https://youtu.be/...',
    fetch: 'Fetch',
    error: "Could not fetch video info. Check the URL and try again.",
    formatLabel: 'Select Format',
    downloading: 'Starting download...',
    downloadAudio: 'Download Audio (.m4a)',
    downloadVideo: (quality: string) => `Download Video — ${quality} .mp4`,
    themeLight: 'Switch to light theme',
    themeDark: 'Switch to dark theme',
    langToggle: 'Türkçeye geç',
  },
} satisfies Record<Lang, Record<string, string | ((...args: string[]) => string)>>

export function getInitialLang(): Lang {
  const saved = localStorage.getItem('lang')
  if (saved === 'tr' || saved === 'en') return saved
  return navigator.language.toLowerCase().startsWith('tr') ? 'tr' : 'en'
}
