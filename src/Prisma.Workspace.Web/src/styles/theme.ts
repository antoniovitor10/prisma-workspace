// theme.ts — Tokens Prisma WorkSpace (D68)
// Fonte: LP Prisma + mockup de login. Espectro violeta → azul → cyan → magenta → laranja.

const spectrum = {
  0: '#7C3AED',
  1: '#2563EB',
  2: '#06B6D4',
  3: '#DB2777',
  4: '#F97316',
} as const;

const font = {
  display: "'Inter', system-ui, sans-serif",
  body: "'Inter', system-ui, sans-serif",
  mono: 'ui-monospace, "SFMono-Regular", Consolas, monospace',
  serif: "'Instrument Serif', Georgia, serif",
} as const;

const fontWeight = { regular: 400, medium: 500, bold: 700, black: 900 } as const;
const fontSize = {
  xs: '0.75rem', sm: '0.875rem', md: '1rem',
  lg: '1.125rem', xl: '1.5rem', xxl: '2rem',
} as const;
const radius = { sm: '6px', md: '10px', lg: '14px', card: '16px', xl: '20px', pill: '999px' } as const;
const space = { 1: '4px', 2: '8px', 2.5: '10px', 3: '12px', 4: '16px', 5: '20px', 6: '24px', 8: '32px', 10: '40px', 12: '48px' } as const;
const transition = 'all 0.2s ease-in-out';

const brandGradient = 'linear-gradient(92deg, #7C3AED, #A855F7 30%, #DB2777 65%, #F97316)';

function buildTheme(mode: 'light' | 'dark') {
  const dark = mode === 'dark';
  return {
    mode,
    color: {
      brand: spectrum[1],
      accentBlue: spectrum[2],
      // Tokens para texto e controles pequenos sobre superfícies claras.
      // O espectro continua sendo usado em bordas, fundos e ilustrações;
      // estes valores evitam contraste insuficiente em textos funcionais.
      accentBlueAccessible: dark ? '#67E8F9' : '#0E7490',
      brandAccessible: dark ? '#93C5FD' : '#1D4ED8',
      brandControlBackground: '#1D4ED8',
      textMutedAccessible: dark ? '#CBD5E1' : '#475569',
      accentGreen: '#10B981',
      accentAmber: spectrum[4],
      accentViolet: spectrum[0],
      accentMagenta: spectrum[3],
      danger: '#E11D48',
      primary: spectrum[1],
      info: spectrum[2],
      success: '#10B981',
      warning: spectrum[4],
      gradient: brandGradient,
      spectrum,

      neutral: dark
        ? {
            0: '#020617',
            50: '#0B1120',
            100: '#0F172A',
            200: '#1E293B',
            300: '#334155',
            400: '#64748B',
            500: '#94A3B8',
            600: '#CBD5E1',
            700: '#E2E8F0',
            800: '#F1F5F9',
            900: '#F8FAFC',
          }
        : {
            0: '#FFFFFF',
            50: '#F8FAFC',
            100: '#F1F5F9',
            200: '#E2E8F0',
            300: '#CBD5E1',
            400: '#94A3B8',
            500: '#64748B',
            600: '#475569',
            700: '#334155',
            800: '#1E293B',
            900: '#0F172A',
          },

      bg: dark ? '#070B17' : '#F6F7FB',
      surface: dark ? '#0E1424' : '#FFFFFF',
      surfaceElevated: dark ? '#141C30' : '#FFFFFF',
      surfaceSubtle: dark ? '#0A1020' : '#F9FAFC',
      border: dark ? 'rgba(148,163,184,0.16)' : '#E5E8F0',
      borderStrong: dark ? 'rgba(148,163,184,0.28)' : '#CDD3E0',
      text: dark ? '#F8FAFC' : '#0F172A',
      textMuted: dark ? '#9AA8BD' : '#667085',
      textSubtle: dark ? '#718096' : '#98A2B3',
      onBrand: '#FFFFFF',
    },
    font,
    fontWeight,
    fontSize,
    radius,
    space,
    transition,
    shadow: dark
      ? {
          sm: '0 1px 2px rgba(0,0,0,0.35)',
          md: '0 8px 24px rgba(0,0,0,0.28)',
          lg: '0 24px 64px rgba(0,0,0,0.38)',
        }
      : {
          sm: '0 1px 2px rgba(15,23,42,0.06)',
          md: '0 8px 24px rgba(20,24,40,0.08)',
          lg: '0 24px 64px rgba(20,24,40,0.12)',
        },
  } as const;
}

export const lightTheme = buildTheme('light');
export const darkTheme = buildTheme('dark');
export const theme = lightTheme;
export type AppTheme = typeof lightTheme;
export type ThemeMode = 'light' | 'dark';
