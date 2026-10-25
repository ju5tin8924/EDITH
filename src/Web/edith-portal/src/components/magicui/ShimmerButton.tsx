import React, { CSSProperties } from 'react';

export interface ShimmerButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  shimmerColor?: string;
  shimmerSize?: string;
  borderRadius?: string;
  shimmerDuration?: string;
  background?: string;
  className?: string;
  children?: React.ReactNode;
}

export const ShimmerButton = React.forwardRef<HTMLButtonElement, ShimmerButtonProps>(
  (
    {
      shimmerColor = '#ffffff',
      shimmerSize = '0.05em',
      shimmerDuration = '3s',
      borderRadius = '0.5rem',
      background = 'linear-gradient(135deg, #1a237e 0%, #283593 100%)',
      className = '',
      children,
      disabled,
      ...props
    },
    ref
  ) => {
    return (
      <button
        ref={ref}
        disabled={disabled}
        style={
          {
            '--spread': '90deg',
            '--shimmer-color': shimmerColor,
            '--radius': borderRadius,
            '--speed': shimmerDuration,
            '--cut': shimmerSize,
            '--bg': background,
            borderRadius: borderRadius,
          } as CSSProperties
        }
        className={`group relative z-0 flex cursor-pointer items-center justify-center overflow-hidden whitespace-nowrap border border-white/20 px-4 py-2 text-white [background:var(--bg)] shadow-xs hover:shadow-lg hover:shadow-primary/25 hover:border-white/35 active:translate-y-0.5 active:shadow-2xs transition-all duration-150 shadow-[inset_0_1px_0_0_rgba(255,255,255,0.25)] disabled:pointer-events-none disabled:opacity-50 ${className}`}
        {...props}
      >
        {/* Spark container */}
        <div className="absolute inset-0 -z-30 overflow-visible [container-type:size]">
          <div className="absolute inset-0 h-[100cqh] animate-shimmer-slide [aspect-ratio:1] [border-radius:0] [mask:none]">
            <div className="animate-spin-around absolute -inset-full w-auto rotate-0 [background:conic-gradient(from_calc(270deg-(var(--spread)*0.5)),transparent_0,var(--shimmer-color)_var(--spread),transparent_var(--spread))] [translate:0_0]" />
          </div>
        </div>

        {/* Shimmer backdrop overlay */}
        <div className="absolute inset-[1px] -z-20 rounded-[inherit] bg-gradient-to-r from-primary to-primary-light transition-opacity" />

        {/* Highlight content */}
        <span className="relative z-10 flex items-center gap-2 font-mono text-xs font-bold tracking-wide">
          {children}
        </span>
      </button>
    );
  }
);

ShimmerButton.displayName = 'ShimmerButton';
