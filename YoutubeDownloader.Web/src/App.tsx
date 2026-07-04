import { useState, useCallback, useEffect } from 'react'
import { translations, getInitialLang, type Lang } from './i18n'

interface StreamOption {
  quality: string
  container: string
  size: string
  type: string
  maxHeight: number
}

interface VideoInfo {
  title: string
  author: string
  thumbnailUrl: string
  durationSeconds: number | null
  availableStreams: StreamOption[]
}

function formatDuration(seconds: number | null): string {
  if (!seconds) return ''
  const h = Math.floor(seconds / 3600)
  const m = Math.floor((seconds % 3600) / 60)
  const s = seconds % 60
  return h > 0
    ? `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
    : `${m}:${String(s).padStart(2, '0')}`
}

function getInitialTheme(): 'dark' | 'light' {
  const saved = localStorage.getItem('theme')
  if (saved === 'dark' || saved === 'light') return saved
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

export default function App() {
  const [theme, setTheme] = useState<'dark' | 'light'>(getInitialTheme)
  const [lang, setLang] = useState<Lang>(getInitialLang)

  const t = translations[lang]

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme)
    localStorage.setItem('theme', theme)
  }, [theme])

  useEffect(() => {
    document.documentElement.setAttribute('lang', lang)
    localStorage.setItem('lang', lang)
  }, [lang])

  const toggleTheme = useCallback(() => {
    setTheme(prev => (prev === 'dark' ? 'light' : 'dark'))
  }, [])

  const toggleLang = useCallback(() => {
    setLang(prev => (prev === 'tr' ? 'en' : 'tr'))
  }, [])

  const [url, setUrl] = useState('')
  const [videoInfo, setVideoInfo] = useState<VideoInfo | null>(null)
  const [selectedStream, setSelectedStream] = useState<StreamOption | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [isDownloading, setIsDownloading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const fetchInfo = useCallback(async () => {
    if (!url.trim()) return
    setIsLoading(true)
    setError(null)
    setVideoInfo(null)
    setSelectedStream(null)
    try {
      const res = await fetch('/api/video/info', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ url: url.trim() }),
      })
      if (!res.ok) throw new Error()
      const data: VideoInfo = await res.json()
      setVideoInfo(data)
      setSelectedStream(
        data.availableStreams.find(s => s.type === 'adaptive' || s.type === 'muxed') ?? null
      )
    } catch {
      setError(t.error)
    } finally {
      setIsLoading(false)
    }
  }, [url, t])

  const startDownload = useCallback(async () => {
    if (!selectedStream || !url.trim()) return
    setIsDownloading(true)
    setError(null)
    try {
      const encodedUrl = encodeURIComponent(url.trim())
      let apiUrl: string

      if (selectedStream.type === 'audio-only') {
        apiUrl = `/api/video/direct-url?url=${encodedUrl}&audioOnly=true`
      } else if (selectedStream.type === 'adaptive') {
        apiUrl = `/api/video/direct-url?url=${encodedUrl}&type=adaptive&maxHeight=${selectedStream.maxHeight}`
      } else {
        apiUrl = `/api/video/direct-url?url=${encodedUrl}&type=muxed&maxHeight=${selectedStream.maxHeight}`
      }

      const res = await fetch(apiUrl)
      if (!res.ok) throw new Error()
      const data = await res.json()

      const link = document.createElement('a')
      link.href = data.url
      link.download = `${data.title}.mp4`
      document.body.appendChild(link)
      link.click()
      document.body.removeChild(link)
    } catch {
      setError('İndirme başlatılamadı.')
    } finally {
      setTimeout(() => setIsDownloading(false), 2000)
    }
  }, [selectedStream, url])

  return (
    <div className="app">
      <div className="toolbar">
        <button
          className="theme-toggle"
          onClick={toggleTheme}
          title={theme === 'dark' ? t.themeLight : t.themeDark}
        >
          {theme === 'dark' ? '☀️' : '🌙'}
        </button>
        <button
          className="lang-toggle"
          onClick={toggleLang}
          title={t.langToggle}
        >
          {lang === 'tr' ? 'EN' : 'TR'}
        </button>
      </div>

      <main className="main">
        <h1 className="title">YouTube Video Downloader</h1>
        <p className="subtitle">{t.subtitle}</p>

        <div className="card url-card">
          <div className="url-row">
            <input
              className="url-input"
              type="text"
              placeholder={t.placeholder}
              value={url}
              onChange={e => setUrl(e.target.value)}
              onKeyDown={e => e.key === 'Enter' && fetchInfo()}
            />
            <button
              className="btn btn-primary fetch-btn"
              onClick={fetchInfo}
              disabled={isLoading}
            >
              {isLoading ? <span className="spinner" /> : t.fetch}
            </button>
          </div>
        </div>

        {error && (
          <div className="alert">
            <span>{error}</span>
            <button className="alert-close" onClick={() => setError(null)}>×</button>
          </div>
        )}

        {videoInfo && (
          <>
            <div className="card video-card">
              <img
                className="thumbnail"
                src={videoInfo.thumbnailUrl}
                alt="thumbnail"
              />
              <div className="video-meta">
                <p className="video-title">{videoInfo.title}</p>
                <p className="video-author">{videoInfo.author}</p>
                {videoInfo.durationSeconds && (
                  <span className="duration-chip">
                    ⏱ {formatDuration(videoInfo.durationSeconds)}
                  </span>
                )}
              </div>
            </div>

            <p className="section-label">{t.formatLabel}</p>
            <div className="stream-grid">
              {videoInfo.availableStreams.map((stream, i) => (
                <button
                  key={i}
                  className={`stream-btn${selectedStream === stream ? ' selected' : ''}`}
                  onClick={() => setSelectedStream(stream)}
                >
                  <span className="stream-quality">{stream.quality}</span>
                  <span className="stream-meta">
                    {stream.container.toUpperCase()} · {stream.size}
                    {stream.type === 'adaptive' && ' · FFmpeg'}
                  </span>
                </button>
              ))}
            </div>

            {selectedStream && (
              <button
                className="btn btn-primary download-btn"
                onClick={startDownload}
                disabled={isDownloading}
              >
                {isDownloading ? (
                  <>
                    <span className="spinner" />
                    <span>{t.downloading}</span>
                  </>
                ) : selectedStream.type === 'audio-only' ? (
                  t.downloadAudio
                ) : (
                  t.downloadVideo(selectedStream.quality)
                )}
              </button>
            )}
          </>
        )}
      </main>

      <footer className="footer">
        <div className="footer-divider" />
        <span className="footer-name">Aybar Özcan</span>
      </footer>
    </div>
  )
}