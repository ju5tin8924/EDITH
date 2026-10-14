import React from 'react';

export const Footer: React.FC = () => {
  return (
    <footer className="h-7 bg-[#1a237e] text-[#c5cae9] border-t border-[#283593] z-40 flex items-center justify-between px-4 font-mono text-[11px] select-none shadow-md shrink-0">
      <div className="flex items-center gap-4">
        <span className="flex items-center gap-1.5">
          <kbd className="px-1.5 py-0.5 bg-[#283593] text-white border border-[#3949ab] rounded text-[10px] font-bold">[1-6]</kbd>
          <span className="text-[#c5cae9] hidden sm:inline">Switch Page</span>
        </span>
        <span className="flex items-center gap-1.5">
          <kbd className="px-1.5 py-0.5 bg-[#283593] text-white border border-[#3949ab] rounded text-[10px] font-bold">[Ctrl+K]</kbd>
          <span className="text-[#c5cae9] hidden sm:inline">Search</span>
        </span>
        <span className="flex items-center gap-1.5">
          <kbd className="px-1.5 py-0.5 bg-[#283593] text-white border border-[#3949ab] rounded text-[10px] font-bold">[Ctrl+E]</kbd>
          <span className="text-[#c5cae9] hidden sm:inline">Export PDF</span>
        </span>
      </div>

      <div className="flex items-center gap-4">
        <span className="text-[#9fa8da] hidden md:inline">HOST: uae-central1-prd04.edith.internal</span>
        <span className="flex items-center gap-1.5 text-[#a7f3d0]">
          <span className="w-1.5 h-1.5 rounded-full bg-accent animate-pulse" />
          <span className="text-white font-medium">FIPS 140-3 Hardware Token Active</span>
        </span>
      </div>
    </footer>
  );
};
