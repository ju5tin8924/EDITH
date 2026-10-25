import React, { useState } from 'react';
import { 
  FileText, 
  CheckCircle2, 
  Download, 
  Send, 
  Copy, 
  Check, 
  Code, 
  Building2, 
  ShieldCheck
} from 'lucide-react';
import { usePageMeta } from '../config/pageMeta';

export const GoAmlReportingView: React.FC = () => {
  const [reportType, setReportType] = useState<'STR' | 'SAR' | 'AIF'>('STR');
  const [isCopied, setIsCopied] = useState(false);
  const [isDispatched, setIsDispatched] = useState(false);

  const mockXml = `<?xml version="1.0" encoding="utf-8"?>
<report xmlns="http://www.unodc.org/goaml" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:schemaLocation="http://www.unodc.org/goaml goaml.xsd">
  <rentity_id>EDITH-BANK-DXB-004</rentity_id>
  <submission_code>${reportType}</submission_code>
  <report_code>${reportType}</report_code>
  <submission_date>2026-10-14T07:15:00Z</submission_date>
  <currency_code_local>AED</currency_code_local>
  <reporting_person>
    <first_name>Fatima</first_name>
    <last_name>Al-Nuaimi</last_name>
    <id_number>784-1988-1294812-1</id_number>
    <role>CHIEF_COMPLIANCE_OFFICER</role>
  </reporting_person>
  <reason>Designated statutory sanctions intercept on OFAC SDN beneficiary Victor Anatolyevich Bout during SWIFT MT103 execution.</reason>
  <action>Transaction frozen under statutory mandate; funds isolated in escrow account pending Financial Intelligence Unit disposition.</action>
  <transaction>
    <transactionnumber>TX-2026-9921448-AE</transactionnumber>
    <internal_ref_number>EDITH-CAS-2026-0042</internal_ref_number>
    <transaction_location>Dubai International Financial Centre (DIFC)</transaction_location>
    <transaction_description>Inbound SWIFT wire settlement</transaction_description>
    <date_transaction>2026-10-14T07:12:00Z</date_transaction>
    <transmode_code>WIRE</transmode_code>
    <amount>450000.00</amount>
    <currency_code>USD</currency_code>
    <t_from_my_client>
      <from_funds_code>ACC</from_funds_code>
      <from_account>
        <institution_name>Abu Dhabi Commercial Bank</institution_name>
        <account>AE820330000001234567890</account>
        <client_name>Rashid Al-Maktoum Trading FZE</client_name>
      </from_account>
    </t_from_my_client>
    <t_to>
      <to_funds_code>ACC</to_funds_code>
      <to_account>
        <institution_name>Banque Privée Zurich</institution_name>
        <account>CH9300762011623852957</account>
        <client_name>Victor Anatolyevich Bout</client_name>
      </to_account>
    </t_to>
  </transaction>
</report>`;

  const handleCopy = () => {
    navigator.clipboard.writeText(mockXml);
    setIsCopied(true);
    setTimeout(() => setIsCopied(false), 2000);
  };

  const handleDispatch = () => {
    setIsDispatched(true);
    setTimeout(() => setIsDispatched(false), 5000);
  };

  const pageMeta = usePageMeta(
    'Authorization',
    'Review and authorize pending sanction alerts, customer risk recalculations, and regulatory filings.'
  );

  return (
    <div className="p-6 space-y-6 max-w-7xl mx-auto">
      {/* Page Header */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-surface p-4 rounded-xl border border-border">
        <div>
          <div className="flex flex-wrap items-center gap-2.5">
            <h1 className="text-lg sm:text-xl font-bold text-ink flex items-center gap-2.5">
              <FileText className="w-5 h-5 text-primary" />
              <span>{pageMeta.title}</span>
            </h1>
            {pageMeta.parent && (
              <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-primary/10 text-primary border border-primary/20">
                {pageMeta.parent}
              </span>
            )}
          </div>
          <p className="text-xs text-muted mt-1">
            {pageMeta.definition}
          </p>
        </div>

        <div className="flex items-center gap-2 font-mono text-xs">
          <div className="px-3 py-1.5 rounded-lg bg-accent/10 border border-accent/30 text-accent font-bold flex items-center gap-2">
            <CheckCircle2 className="w-4 h-4" />
            <span>XSD Schema 4.0: Valid (0 Errors)</span>
          </div>
        </div>
      </div>

      {/* Dispatch Success Alert */}
      {isDispatched && (
        <div className="p-4 bg-accent/10 border border-accent text-accent rounded-xl font-mono text-xs flex items-center justify-between">
          <div className="flex items-center gap-3">
            <ShieldCheck className="w-5 h-5 shrink-0" />
            <div>
              <div className="font-bold">Filing Dispatched to Financial Intelligence Unit (FIU)</div>
              <div className="text-[11px] text-accent/80">Transmission Receipt: FIU-UAE-2026-99214 • Cryptographic HMAC-SHA256 verified</div>
            </div>
          </div>
          <span className="text-[10px] font-bold px-2 py-0.5 rounded bg-accent text-white">FIU ACCEPTED</span>
        </div>
      )}

      {/* Main Grid: Form Left, XML Right */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        {/* Form Controls & Validation (5 cols) */}
        <div className="lg:col-span-5 bg-surface rounded-xl border border-border p-5 space-y-4 font-mono text-xs">
          <div className="flex items-center justify-between pb-3 border-b border-border">
            <h3 className="text-xs uppercase tracking-wider font-bold text-ink flex items-center gap-2">
              <Building2 className="w-4 h-4 text-primary" />
              Filing Configuration
            </h3>
            <span className="text-[10px] text-muted">Mandatory GCC Fields</span>
          </div>

          <div className="space-y-3">
            <div>
              <label className="text-muted text-[10px] uppercase font-bold block mb-1">Report Category</label>
              <div className="grid grid-cols-3 gap-2">
                {(['STR', 'SAR', 'AIF'] as const).map(type => (
                  <button
                    key={type}
                    onClick={() => setReportType(type)}
                    className={`py-2 rounded font-bold transition-all border ${
                      reportType === type
                        ? 'bg-primary text-white border-primary shadow-xs'
                        : 'bg-surface-muted text-ink border-border hover:bg-surface-hover'
                    }`}
                  >
                    {type}
                  </button>
                ))}
              </div>
            </div>

            <div>
              <label className="text-muted text-[10px] uppercase font-bold block mb-1">Reporting Institution</label>
              <input
                type="text"
                readOnly
                value="Abu Dhabi Commercial Bank (ADCB) - Central Hub"
                className="w-full p-2 rounded border border-border bg-surface-muted text-ink font-semibold"
              />
            </div>

            <div>
              <label className="text-muted text-[10px] uppercase font-bold block mb-1">Source Investigation Case</label>
              <div className="p-2.5 rounded border border-border bg-surface-muted space-y-1">
                <div className="flex justify-between items-center">
                  <span className="font-bold text-primary">EDITH-CAS-2026-0042</span>
                  <span className="px-2 py-0.5 rounded text-[10px] bg-critical text-white font-bold">CRITICAL</span>
                </div>
                <div className="text-[11px] text-muted">Subject: Viktor Bout • Wire $450,000 USD</div>
              </div>
            </div>

            <div>
              <label className="text-muted text-[10px] uppercase font-bold block mb-1">Investigative Grounds (FATF Narrative)</label>
              <div className="p-3 rounded border border-border bg-surface-muted text-[11px] text-ink leading-relaxed">
                Wire intercepted under statutory freeze rules. Beneficiary is explicitly matched to OFAC SDN designation for Viktor Anatolyevich Bout (Arms trafficking / UN Res 1521). Funds quarantined immediately.
              </div>
            </div>

            <div>
              <label className="text-muted text-[10px] uppercase font-bold block mb-1">Certified Compliance Officer</label>
              <div className="p-2 rounded border border-border bg-surface-muted text-[11px] flex justify-between items-center">
                <span>Fatima Al-Nuaimi (CCO // Unit 04)</span>
                <span className="text-accent font-bold">FIPS TOKEN PIN OK</span>
              </div>
            </div>

            <div className="pt-3 border-t border-border">
              <button
                onClick={handleDispatch}
                disabled={isDispatched}
                className="w-full py-2.5 rounded bg-primary text-white font-bold text-xs hover:bg-primary-hover flex items-center justify-center gap-2 shadow-xs transition-all disabled:opacity-50"
              >
                <Send className="w-4 h-4" />
                <span>Sign & Dispatch to FIU Terminal</span>
              </button>
            </div>
          </div>
        </div>

        {/* XML Live Code Canvas (7 cols) */}
        <div className="lg:col-span-7 bg-[#0b0f19] rounded-xl border border-border p-5 flex flex-col justify-between font-mono text-xs overflow-hidden">
          <div className="flex items-center justify-between pb-3 border-b border-slate-800 text-slate-300">
            <div className="flex items-center gap-2">
              <Code className="w-4 h-4 text-accent" />
              <span className="text-white font-bold text-xs">goaml_payload_{reportType.toLowerCase()}_20261014.xml</span>
            </div>

            <div className="flex items-center gap-2">
              <button
                onClick={handleCopy}
                className="px-2.5 py-1 rounded bg-slate-800 hover:bg-slate-700 text-slate-200 text-[11px] flex items-center gap-1.5 transition-colors"
              >
                {isCopied ? <Check className="w-3.5 h-3.5 text-accent" /> : <Copy className="w-3.5 h-3.5" />}
                <span>{isCopied ? 'Copied' : 'Copy XML'}</span>
              </button>

              <button
                onClick={() => {
                  const blob = new Blob([mockXml], { type: 'application/xml' });
                  const url = URL.createObjectURL(blob);
                  const a = document.createElement('a');
                  a.href = url;
                  a.download = `goaml_${reportType}_20261014.xml`;
                  a.click();
                }}
                className="px-2.5 py-1 rounded bg-slate-800 hover:bg-slate-700 text-slate-200 text-[11px] flex items-center gap-1.5 transition-colors"
              >
                <Download className="w-3.5 h-3.5" />
                <span>Export</span>
              </button>
            </div>
          </div>

          <pre className="my-4 overflow-x-auto text-[11px] text-emerald-400/90 leading-relaxed max-h-[500px]">
            {mockXml}
          </pre>

          <div className="pt-3 border-t border-slate-800 flex items-center justify-between text-[10px] text-slate-400">
            <span>Encoding: UTF-8 • XSD Schema: goaml.xsd • GCC Spec</span>
            <span className="text-accent font-bold">Digital Signature: SHA256-RSA-4096</span>
          </div>
        </div>
      </div>
    </div>
  );
};
