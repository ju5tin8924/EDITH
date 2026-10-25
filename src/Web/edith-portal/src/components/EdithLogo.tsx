import React, { useId } from 'react';

export interface EdithLogoProps {
  variant?: 'dark' | 'light' | 'gold';
  showWordmark?: boolean;
  animated?: boolean;
  className?: string;
}

const OUTER_ARC_PATH =
  'M494.3 134.3L529 134L562.9 139.1L600.7 151.3L630.7 167.3L659.2 188.8L683.2 213.8L705.7 247.3L718.7 275.3L718.6 280.6L706.7 287.7L701.8 287.2L687.7 258.3L666.2 226.8L647.2 206.8L615.7 183.3L594.7 172.3L570.7 163.3L531 156L505 155L468.1 160.1L449.3 165.3L419.3 178.3L383.8 202.8L360.8 225.8L341.3 253.3L324.5 287.5L320.3 287.7L308.4 281.6L307.1 277.1L313.3 262.3L332.3 228.3L353.3 202.3L381.8 176.8L405.3 161.3L435.3 147.3L463.1 139.1Z';

// Ordered left-to-right along the inner radar arc for sequential sweep animation
const RADAR_DASHES_ORDERED = [
  'M375.4 277.4L378.6 277.4L381.8 281L371.3 300.3L367.3 299.7L365.1 296.1Z',
  'M402.4 243.4L407.2 245.8L407.6 249.6L393.6 263.6L390.3 262.8L388.3 258.3Z',
  'M437.4 217.4L441.2 218.8L442.6 223.6L425.7 234.7L421.3 232.7L421 228Z',
  'M476.3 202.3L481.5 202.5L482.2 209.2L465 215L460 214L459.8 207.8Z',
  'M503.4 198.4L523.6 198.4L525 204L522.9 206.9L503.4 206.6L501 203Z',
  'M545.4 202.4L566.7 207.3L568 210L566.2 214.2L561.1 214.9L545 210Z',
  'M586.4 217.4L589.6 217.4L604.8 227.3L605.9 231.9L602.9 234.9L584.4 224.6L583.5 222.2Z',
  'M621.4 243.4L625.2 243.8L637.7 257.3L638.5 259.8L635.3 263.3L631.8 263.2L618.8 249.2L618.4 246.4Z',
  'M648.3 277.3L653.2 278.8L658.7 289.3L661.8 297L658.3 300.3L653.8 299.2L645.3 282.7L645.4 279.4Z',
];

const HEAD_AND_BEAK_PATHS = [
  'M507.3 286.3L524.9 287.1L557.1 294.9L575.5 289.5L575.9 297.9L570.1 311.9L573.9 312.9L579 310.2L580 316L574.3 328.3L574.1 334.9L579.3 350.7L590.9 371.1L582.3 375.3L583 391L580.2 401.2L569.4 389.6L567.9 401.9L562.7 416.7L559.8 419.2L555.6 409.4L552.8 407.8L545.7 427.7L523.8 450.8L514.2 467.2L511.3 465.7L502.2 449.8L482.3 429.7L478.3 422.7L474.2 407.8L471.8 408.8L466 419L459.1 403.9L457.6 388.6L446.3 401.4L444 394L444.6 373.4L442.8 371.5L437.1 372.9L436 371L447.7 350.7L452.9 333.9L446 313L447 310.2L454.1 312.9L456.8 312L451.1 297.9L450.8 289.8L470 295Z M553.6 324.6L548.3 326.3L534.7 337.7L525 342.8L513 334.2L506.7 340.7L499.3 341.7L474.3 325.4L474.1 335.9L476.8 343.2L480.4 346.6L493.8 350L489.7 354.7L476.6 359.4L487.3 371.8L513.7 411.4L530.3 383.3L550.5 359.8L537.3 354.7L533.2 350L544.7 347.7L549.2 344.2L551.9 338.9Z',
  'M545.3 333.5L546.9 335.1L546.2 341.2L537.4 344.6L535.8 341.8Z',
  'M480.5 334.5L487.8 338.3L490.6 341.3L489.8 343.8L486.1 344.9L482.3 342.8Z',
  'M512.3 342.3L522.2 347.8L539.8 363L525.3 381.3L514 402L496.7 374.3L487.2 363Z',
];

const PRIMARY_WINGS_PATH =
  'M866.3 139.5L870.7 146.3L877.9 171.1L880 196L877.9 213.9L872.7 231.7L864.7 248.7L857.2 259.2L837.2 279.2L809.7 298.7L786.7 311.7L680.3 363.3L648.3 382.3L624.3 401.3L606.3 423.3L590.7 462.7L579.7 482.7L569.2 493.2L543.3 511.3L533.3 522.3L529.1 531.9L530.7 532.6L580 498.2L580.9 502.9L576.7 511.7L531.8 554.8L513.7 584.4L493.2 552.8L477.2 536.8L458.3 521.8L448.3 508.7L446.1 502.9L446.8 498.5L498.6 535.4L495.7 526.3L486.6 514.4L454.8 491.2L445.3 480.7L436.3 463.7L425.7 433.3L411.8 411.3L391.2 391.8L358.7 370.3L250 317L215.3 297.7L191.8 281.2L171.8 262.2L161 247L150.1 220.9L146 194L149.1 167.1L159.3 140.4L172.3 180.7L185.3 204.7L199.3 222.8L218.8 242.2L245.3 262.7L363.7 332.3L402.2 359.8L416.7 376.3L439.8 415.2L486.2 454.8L497.7 468.3L511.3 494.7L513.9 495.9L525.3 473.3L534.8 460.8L552.8 442.8L588.2 414.2L609.3 377.3L628.8 355.8L664.3 331.3L767.7 271.7L799.2 249.2L821.8 228.8L836.7 210.7L850.7 187.7L860.7 161.7Z';

const SECONDARY_FEATHERS_PATHS = [
  'M132.3 247.4L134.7 248.3L146.3 268.7L161.8 288.2L197.3 318.7L232.3 339.7L354.7 403.3L386.2 425.8L392.5 433.2L389 434L319.1 422.9L252.1 407.9L207.3 392.7L174.3 374.7L149.8 351.2L142.3 339.7L133.3 318.7L128 288L129.1 263.1Z',
  'M892.4 247.4L894.5 247.5L895.9 251.1L898 267L898 294L893.7 316.7L889.7 328.7L876.7 351.7L853.2 374.2L819.7 392.7L781.9 405.9L708.9 422.9L639.1 434.1L636.1 434.9L634.1 432.9L637.8 427.8L673.3 402.3L797.7 337.7L836.2 313.2L866.2 286.2L875.7 274.7Z',
  'M149.5 386.5L177 405L216.3 420.7L356.4 451.4L346 455L268 466L229.1 465.9L199.3 458.7L179.8 447.2L165.3 431.7L155.3 411.7L149.1 391.9Z',
  'M876.2 386.5L877.9 389.9L871.7 411.7L860.7 432.7L848.2 446.2L827.7 458.7L805.9 464.9L771 467L728.1 462.9L682.7 455.3L669.6 451.3L813.7 419.7L851.7 403.7Z',
  'M399.3 465.3L404.5 465.8L389.7 476.7L360.7 491.7L337.7 500.7L308.9 507.9L277.1 508.9L261.3 505.7L241 497L227.8 488.2L225.8 483.8L261 485L286.9 482.9Z',
  'M621.6 465.4L629.9 465.1L743 483L771 485L800.5 483.5L798.8 487.8L789.2 495.2L776.7 501.7L754.9 507.9L717.1 507.9L692.3 501.7L663.3 490.7L637.3 476.7Z',
  'M415.3 482.3L417.5 482.5L417.6 484.6L404.8 502.8L385.2 521.2L367.7 531.7L344.9 538.9L326 539L302.7 531.3L293.8 526.2L292.8 522.8L317.9 519.9L339.7 514.7L367.7 504.7Z',
  'M610.5 483.5L656.3 503.7L687.3 514.7L733.5 523.4L731.2 527.2L725.3 530.3L711.7 536.7L701 539L682.1 538.9L663.3 533.7L637.3 517.8L618.8 498.2Z',
];

const TAIL_FEATHERS_PATHS = [
  'M455.3 543.6L457 547L455.9 570.9L448.7 595.7L438.7 614.7L426.2 631.2L404.7 651.7L398.3 652.7L389.2 646.8L381.4 639.3L405.8 618.8L426.2 596.2L442.7 571.7Z',
  'M570.4 544.6L587.3 577.7L602.8 599.2L624.3 621.8L644.6 639.3L637.7 646.7L628.6 652.6L623.4 652.6L606.3 637.8L588.3 615.7L580.3 601.7L573.1 581.9L569 559Z',
  'M471.3 567.6L474 578L472.9 599.9L469.9 615.9L461.7 639.7L439.2 676.2L434.4 676.6L424.8 670.2L415.5 662.3L432.2 643.2L451.7 614.7L461.7 594.7Z',
  'M555.2 568.5L574.3 613.7L595.8 645.2L610.6 661.4L609.8 664.8L602 670L591.7 676.7L587.8 676.2L578.3 663.7L564.3 637.7L554.1 602.9L553 577Z',
  'M539.3 590.5L551.3 636.7L565 666L575.9 684.1L574.6 687.6L563.2 697.2L553.7 704.6L550.3 702.7L537.1 659.9L534 640L534 613Z',
  'M487.3 591.5L490.9 599.1L493 618L492 645L488.9 662.9L476.7 702.7L473.3 704.6L464.3 697.8L452.8 688.2L451.1 684.1L474.7 638.7L482.9 612.9Z',
  'M522.2 616.5L524.1 653.9L531.9 696.9L513.8 716.5L502.3 705.8L494 695L502.9 652.9L504.1 618.1L507.2 618.8L513.8 629.5Z',
];

const WORDMARK_EDITH_PATHS = [
  'M279.4 773.4L353 773L358.6 776.4L357.6 804.6L342 802L317 802L313.4 804.4L313.1 816.9L315.3 818.7L332 819L346.2 816.8L347 848L345 851L332 848L315.3 848.3L313.1 850.1L313 860L313.8 864.2L318 866L340 866L358.2 862.8L358.2 894.2L279.5 894.5L282 869L282 791L278.1 775.9Z',
  'M380.4 773.4L451 773L460.7 776.3L464.7 780.3L468 789L468 879L464.7 887.7L460 892L450 895L379.4 893.6L383 876L383 794L379.1 776.9Z M418.1 802.1L414 807L414.1 861.9L419.1 865.9L432.3 865.3L435.2 864.2L437 860L436.9 806.1L432.9 802.1Z',
  'M489.4 773.4L524 773L526.9 775.1L523 794L523 874L526.9 890.1L525.9 893.9L493 895L489.1 892.9L492 868L492 791L488.1 775.9Z',
  'M548.4 773.4L633 773L636.6 774.4L636.5 804.5L612 802L607.1 806.1L607 875L610.9 890.1L609.6 894.6L576 895L572.5 893.5L576 876L575.9 807.1L574.5 803.5L571 802L546.8 804.2L546 777Z',
  'M657.4 773.4L694.2 773.8L691 792L691 815L692.3 817.7L697 819L709.9 818.9L713.9 815.9L714 790L710.8 773.8L748.2 773.8L745 794L745 874L749 892L746.3 894.3L710.5 894.5L714 879L714 852L712.2 848.8L709 848L694.1 848.1L691.1 851.1L691 874L694.2 894.2L660 895L656.5 893.5L660 876L660 791L656.2 775Z',
];

export const EdithLogo: React.FC<EdithLogoProps> = ({
  variant = 'light',
  showWordmark = false,
  animated = false,
  className = 'w-8 h-8',
}) => {
  const uid = useId().replace(/:/g, '');
  const viewBox = showWordmark ? '110 115 805 800' : '110 115 805 620';

  const palette =
    variant === 'dark'
      ? {
          primaryStart: '#ffe082',
          primaryMid: '#ffd54f',
          primaryEnd: '#f59e0b',
          secondaryStart: '#ffffff',
          secondaryEnd: '#9fa8da',
          arcStart: '#34d399',
          arcEnd: '#059669',
          dashColor: '#ffd54f',
          wordmarkColor: '#ffffff',
        }
      : variant === 'gold'
      ? {
          primaryStart: '#ffe082',
          primaryMid: '#ffd54f',
          primaryEnd: '#d97706',
          secondaryStart: '#ffd54f',
          secondaryEnd: '#b45309',
          arcStart: '#34d399',
          arcEnd: '#10b981',
          dashColor: '#fde68a',
          wordmarkColor: '#ffd54f',
        }
      : {
          primaryStart: '#0d1333',
          primaryMid: '#1a237e',
          primaryEnd: '#283593',
          secondaryStart: '#283593',
          secondaryEnd: '#5c6bc0',
          arcStart: '#059669',
          arcEnd: '#10b981',
          dashColor: '#d97706',
          wordmarkColor: '#0d1333',
        };

  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      viewBox={viewBox}
      fill="none"
      className={className}
      aria-label="EDITH AML Intelligence Emblem"
    >
      <defs>
        <linearGradient id={`primary-${uid}`} x1="146" y1="139" x2="880" y2="716" gradientUnits="userSpaceOnUse">
          <stop offset="0%" stopColor={palette.primaryStart} />
          <stop offset="50%" stopColor={palette.primaryMid} />
          <stop offset="100%" stopColor={palette.primaryEnd} />
        </linearGradient>
        <linearGradient id={`secondary-${uid}`} x1="128" y1="247" x2="898" y2="716" gradientUnits="userSpaceOnUse">
          <stop offset="0%" stopColor={palette.secondaryStart} />
          <stop offset="100%" stopColor={palette.secondaryEnd} />
        </linearGradient>
        <linearGradient id={`arc-${uid}`} x1="307" y1="134" x2="719" y2="288" gradientUnits="userSpaceOnUse">
          <stop offset="0%" stopColor={palette.arcStart} />
          <stop offset="100%" stopColor={palette.arcEnd} />
        </linearGradient>
      </defs>

      {/* Outer Radar Horizon Arc */}
      <path
        fill={`url(#arc-${uid})`}
        d={OUTER_ARC_PATH}
        className={animated ? 'animate-edith-arc-glow' : undefined}
      />

      {/* Inner 9-Segment Radar Telemetry Arc */}
      <g fill={palette.dashColor}>
        {RADAR_DASHES_ORDERED.map((d, idx) => (
          <path
            key={idx}
            d={d}
            className={animated ? 'animate-edith-radar-dash' : undefined}
            style={animated ? { animationDelay: `${idx * 110}ms` } : undefined}
          />
        ))}
      </g>

      {/* Eagle Head, Fierce Eyes & Diamond Beak */}
      <g fill={`url(#primary-${uid})`} fillRule="evenodd">
        {HEAD_AND_BEAK_PATHS.map((d, idx) => (
          <path key={idx} d={d} />
        ))}
      </g>

      {/* Primary Sweeping Wings & V-Shield Breast */}
      <path
        fill={`url(#primary-${uid})`}
        d={PRIMARY_WINGS_PATH}
        className={animated ? 'animate-edith-wing-breathe' : undefined}
      />

      {/* Secondary Wing Pinions */}
      <g fill={`url(#secondary-${uid})`}>
        {SECONDARY_FEATHERS_PATHS.map((d, idx) => (
          <path
            key={idx}
            d={d}
            className={animated ? 'animate-edith-feather-wave' : undefined}
            style={animated ? { animationDelay: `${Math.floor(idx / 2) * 140}ms` } : undefined}
          />
        ))}
      </g>

      {/* 7 Tail Feathers */}
      <g fill={`url(#secondary-${uid})`}>
        {TAIL_FEATHERS_PATHS.map((d, idx) => (
          <path
            key={idx}
            d={d}
            className={animated ? 'animate-edith-feather-wave' : undefined}
            style={animated ? { animationDelay: `${idx * 90}ms` } : undefined}
          />
        ))}
      </g>

      {/* Optional Bold Slab "EDITH" Wordmark */}
      {showWordmark && (
        <g fill={palette.wordmarkColor} fillRule="evenodd">
          {WORDMARK_EDITH_PATHS.map((d, idx) => (
            <path key={idx} d={d} />
          ))}
        </g>
      )}
    </svg>
  );
};

export interface EdithLoaderProps {
  label?: string;
  sublabel?: string;
  size?: 'sm' | 'md' | 'lg';
  variant?: 'dark' | 'light';
  overlay?: boolean;
}

export const EdithLoader: React.FC<EdithLoaderProps> = ({
  label = 'PROCESSING REQUEST...',
  sublabel = 'Please wait a moment while your workspace updates',
  size = 'md',
  variant = 'light',
  overlay = false,
}) => {
  const sizeClasses =
    size === 'sm' ? 'w-12 h-12' : size === 'lg' ? 'w-28 h-28' : 'w-20 h-20';

  const content = (
    <div className="flex flex-col items-center justify-center gap-3 select-none">
      <div className="relative flex items-center justify-center">
        {/* Ambient Radar Ring Pulse */}
        <div
          className={`absolute inset-0 rounded-full blur-md animate-ping opacity-20 ${
            variant === 'dark' ? 'bg-amber-400' : 'bg-primary'
          }`}
          style={{ animationDuration: '2.4s' }}
        />
        <div
          className={`relative rounded-2xl p-3 border shadow-lg ${
            variant === 'dark'
              ? 'bg-[#0d1333]/95 border-[#3949ab]'
              : 'bg-white/95 border-border'
          }`}
        >
          <EdithLogo
            variant={variant === 'dark' ? 'dark' : 'light'}
            showWordmark={size === 'lg'}
            animated={true}
            className={sizeClasses}
          />
        </div>
      </div>

      {label && (
        <div className="text-center space-y-1">
          <div
            className={`text-xs font-mono font-bold tracking-widest uppercase flex items-center justify-center gap-2 ${
              variant === 'dark' ? 'text-white' : 'text-ink'
            }`}
          >
            <span className="w-2 h-2 rounded-full bg-accent animate-pulse" />
            <span>{label}</span>
          </div>
          {sublabel && (
            <div
              className={`text-[10px] font-mono ${
                variant === 'dark' ? 'text-[#9fa8da]' : 'text-muted'
              }`}
            >
              {sublabel}
            </div>
          )}
        </div>
      )}
    </div>
  );

  if (overlay) {
    return (
      <div className="fixed inset-0 z-[9998] bg-[#0d1333]/65 backdrop-blur-xs flex items-center justify-center p-4 animate-in fade-in duration-150">
        <div className="bg-white border border-border rounded-2xl shadow-2xl px-8 py-6">
          {content}
        </div>
      </div>
    );
  }

  return content;
};
