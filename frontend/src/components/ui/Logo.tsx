import React from 'react';

interface LogoProps {
  size?: 'header' | 'mobile';
  variant?: 'dark' | 'light';
  className?: string;
}

export const Logo: React.FC<LogoProps> = ({ size = 'header', variant = 'dark', className = '' }) => {
  const color = variant === 'light' ? '#ffffff' : '#2d6a4f';

  if (size === 'mobile') {
    return (
      <svg width="32" height="32" viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg" className={className} aria-label="קפה ארז">
        {/* Coffee cup body */}
        <rect x="6" y="12" width="16" height="14" rx="3" fill={color} />
        {/* Handle */}
        <path d="M22 15 Q28 15 28 19 Q28 23 22 23" stroke={color} strokeWidth="2.5" fill="none" strokeLinecap="round" />
        {/* Steam lines */}
        <path d="M10 9 Q11 7 10 5" stroke={color} strokeWidth="1.5" fill="none" strokeLinecap="round" />
        <path d="M14 9 Q15 6 14 4" stroke={color} strokeWidth="1.5" fill="none" strokeLinecap="round" />
        <path d="M18 9 Q19 7 18 5" stroke={color} strokeWidth="1.5" fill="none" strokeLinecap="round" />
        {/* Saucer */}
        <ellipse cx="14" cy="26.5" rx="10" ry="2" fill={color} opacity="0.4" />
      </svg>
    );
  }

  return (
    <div className={`flex items-center gap-2 ${className}`} aria-label="קפה ארז">
      <svg width="36" height="36" viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg">
        <rect x="6" y="12" width="16" height="14" rx="3" fill={color} />
        <path d="M22 15 Q28 15 28 19 Q28 23 22 23" stroke={color} strokeWidth="2.5" fill="none" strokeLinecap="round" />
        <path d="M10 9 Q11 7 10 5" stroke={color} strokeWidth="1.5" fill="none" strokeLinecap="round" />
        <path d="M14 9 Q15 6 14 4" stroke={color} strokeWidth="1.5" fill="none" strokeLinecap="round" />
        <path d="M18 9 Q19 7 18 5" stroke={color} strokeWidth="1.5" fill="none" strokeLinecap="round" />
        <ellipse cx="14" cy="26.5" rx="10" ry="2" fill={color} opacity="0.4" />
      </svg>
      <span style={{ fontFamily: 'Rubik, sans-serif', fontWeight: 700, color, fontSize: '1.25rem', direction: 'rtl' }}>
        קפה ארז
      </span>
    </div>
  );
};

export default Logo;
