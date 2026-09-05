import styled from 'styled-components';

const Mark = styled.svg<{ $size: number }>`
  width: ${({ $size }) => $size}px;
  height: ${({ $size }) => $size}px;
  flex: 0 0 auto;
  display: block;
`;

/** Prisma prism mark (LP oficial). */
export function BrandMark({ size = 28, className }: { size?: number; className?: string }) {
  return (
    <Mark className={className} $size={size} viewBox="0 0 32 32" fill="none" aria-hidden="true">
      <path d="M16 2 L5 17 L16 14 Z" fill="#8B5CF6" />
      <path d="M16 2 L27 17 L16 14 Z" fill="#3B82F6" />
      <path d="M5 17 L16 30 L16 14 Z" fill="#5B21B6" />
      <path d="M27 17 L16 30 L16 14 Z" fill="#1D4ED8" />
      <path d="M13.5 25.5 L18.5 25.5 L16 30 Z" fill="#F97316" />
    </Mark>
  );
}

/** Prisma hero gem used on the login branding panel. */
export function BrandGem({ size = 120 }: { size?: number }) {
  return (
    <svg width={size} height={Math.round(size * 1.2)} viewBox="0 0 240 288" fill="none" aria-hidden="true">
      <defs>
        <linearGradient id="prismaGemTL" x1="120" y1="24" x2="40" y2="150" gradientUnits="userSpaceOnUse">
          <stop offset="0" stopColor="#EDE9FE" /><stop offset=".45" stopColor="#C4B5FD" /><stop offset="1" stopColor="#7C3AED" />
        </linearGradient>
        <linearGradient id="prismaGemTR" x1="120" y1="24" x2="206" y2="150" gradientUnits="userSpaceOnUse">
          <stop offset="0" stopColor="#DBEAFE" /><stop offset=".45" stopColor="#93C5FD" /><stop offset="1" stopColor="#2563EB" />
        </linearGradient>
        <linearGradient id="prismaGemBL" x1="70" y1="140" x2="120" y2="302" gradientUnits="userSpaceOnUse">
          <stop offset="0" stopColor="#A78BFA" /><stop offset="1" stopColor="#5B21B6" />
        </linearGradient>
        <linearGradient id="prismaGemBR" x1="170" y1="140" x2="120" y2="302" gradientUnits="userSpaceOnUse">
          <stop offset="0" stopColor="#22D3EE" /><stop offset=".45" stopColor="#2563EB" /><stop offset="1" stopColor="#7C3AED" />
        </linearGradient>
        <linearGradient id="prismaGemCore" x1="84" y1="120" x2="162" y2="220" gradientUnits="userSpaceOnUse">
          <stop stopColor="#F97316" /><stop offset=".36" stopColor="#DB2777" /><stop offset="1" stopColor="#4F46E5" />
        </linearGradient>
        <filter id="prismaGemShadow" x="0" y="0" width="240" height="288" filterUnits="userSpaceOnUse">
          <feDropShadow dx="0" dy="14" stdDeviation="12" floodColor="#4F46E5" floodOpacity=".2" />
        </filter>
      </defs>
      <g filter="url(#prismaGemShadow)">
        <path d="M120 16 L34 146 L120 124 Z" fill="url(#prismaGemTL)" />
        <path d="M120 16 L206 146 L120 124 Z" fill="url(#prismaGemTR)" />
        <path d="M34 146 L120 272 L120 124 Z" fill="url(#prismaGemBL)" />
        <path d="M206 146 L120 272 L120 124 Z" fill="url(#prismaGemBR)" />
        <path d="M34 146 L84 114 L120 124 L74 188 Z" fill="#F97316" fillOpacity=".82" />
        <path d="M74 188 L120 124 L120 272 Z" fill="url(#prismaGemCore)" fillOpacity=".92" />
        <path d="M120 124 L166 188 L120 272 Z" fill="#4338CA" fillOpacity=".72" />
        <path d="M120 124 L206 146 L166 188 Z" fill="#7C3AED" fillOpacity=".66" />
        <path d="M120 16 L84 114 L120 124 Z" fill="#FFFFFF" fillOpacity=".2" />
        <path d="M120 16 L206 146 L120 124 L34 146 Z" stroke="#FFFFFF" strokeOpacity=".28" />
        <path d="M34 146 L120 272 L206 146" stroke="#FFFFFF" strokeOpacity=".18" />
      </g>
    </svg>
  );
}
