import React, { useState } from 'react';
import { 
  Layers, 
  UploadCloud, 
  RefreshCw, 
  CheckCircle, 
  AlertTriangle, 
  FileSpreadsheet,
  Clock
} from 'lucide-react';
import { BulkJobSummary } from '../types';
import { EdithApi } from '../services/api';

export const BulkScreeningView: React.FC = () => {
  const [jobs, setJobs] = useState<BulkJobSummary[]>([
    {
      jobId: 'bulk-job-901',
      fileName: 'ADCB_Customer_Base_Q1_2026.csv',
      status: 'COMPLETED',
      totalRecords: 125000,
      processedRecords: 125000,
      hitCount: 41,
      percentComplete: 100,
      startedAtUtc: new Date(Date.now() - 3600000 * 5).toISOString(),
      completedAtUtc: new Date(Date.now() - 3600000 * 4.8).toISOString()
    },
    {
      jobId: 'bulk-job-902',
      fileName: 'Remittance_Batch_SWIFT_MT103.csv',
      status: 'PROCESSING',
      totalRecords: 50000,
      processedRecords: 38500,
      hitCount: 12,
      percentComplete: 77,
      startedAtUtc: new Date(Date.now() - 600000).toISOString()
    }
  ]);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [deltaSyncRunning, setDeltaSyncRunning] = useState(false);

  const handleSimulateUpload = async () => {
    if (isSubmitting) return;
    setIsSubmitting(true);
    try {
      const newJob = await EdithApi.submitBulkJob('Corporate_Onboarding_Batch_2026.csv', 10000);
      setJobs([newJob, ...jobs]);
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleTriggerDelta = () => {
    setDeltaSyncRunning(true);
    setTimeout(() => {
      setDeltaSyncRunning(false);
      alert('Delta Rescreening Completed: Evaluated 175,000 customers against UN Sanctions Update 2026-Q1. 3 new potential matches identified.');
    }, 2000);
  };

  return (
    <div className="p-6 space-y-6 max-w-7xl mx-auto">
      {/* Title Bar */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-surface p-4 rounded-xl border border-border">
        <div>
          <h1 className="text-xl font-bold text-white flex items-center gap-2.5">
            <Layers className="w-6 h-6 text-primary" />
            P6 High-Throughput Bulk & Delta Rescreening Hub
          </h1>
          <p className="text-xs text-slate-400 mt-1">
            Asynchronous streaming batch ingestion with HTTP 202 submission, zero Redis dependency, and delta re-indexing.
          </p>
        </div>

        <button
          onClick={handleTriggerDelta}
          disabled={deltaSyncRunning}
          className="px-4 py-2 rounded-lg bg-primary/20 text-primary border border-primary/40 hover:bg-primary/30 font-mono text-xs font-semibold flex items-center gap-2 transition disabled:opacity-50"
        >
          <RefreshCw className={`w-4 h-4 ${deltaSyncRunning ? 'animate-spin' : ''}`} />
          {deltaSyncRunning ? 'Running Delta Rescreening...' : 'Trigger Catalog Delta Rescreening'}
        </button>
      </div>

      {/* Upload Drag & Drop Sandbox */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-1 bg-surface rounded-xl border border-border p-5 space-y-4">
          <h2 className="text-xs font-mono uppercase tracking-wider font-semibold text-slate-300 pb-2 border-b border-border flex items-center gap-2">
            <UploadCloud className="w-4 h-4 text-primary" />
            Submit Batch Screening
          </h2>

          <div 
            onClick={handleSimulateUpload}
            className={`border-2 border-dashed border-border hover:border-primary/50 transition rounded-xl p-8 text-center cursor-pointer space-y-3 bg-slate-950/40 ${isSubmitting ? 'opacity-50 pointer-events-none' : ''}`}
          >
            <div className="w-12 h-12 rounded-full bg-primary/10 border border-primary/20 text-primary flex items-center justify-center mx-auto">
              <FileSpreadsheet className="w-6 h-6" />
            </div>
            <div>
              <p className="text-sm font-semibold text-slate-200">Click to Ingest Sample CSV</p>
              <p className="text-xs text-slate-400 mt-0.5">Supports CSV, TSV (up to 500k rows/file)</p>
            </div>
            <div className="text-[10px] font-mono text-slate-500">
              Columns: CustomerId, FullName, Country, DOB, NationalId
            </div>
          </div>

          <div className="p-3 bg-slate-900/60 rounded-lg border border-border text-xs text-slate-400 space-y-1.5 font-mono">
            <div className="flex justify-between">
              <span>Streaming Chunk:</span>
              <span className="text-slate-200">5,000 records</span>
            </div>
            <div className="flex justify-between">
              <span>Worker Concurrency:</span>
              <span className="text-slate-200">8 Parallels</span>
            </div>
            <div className="flex justify-between">
              <span>Estimated Throughput:</span>
              <span className="text-accent font-semibold">12,000+ rec/sec</span>
            </div>
          </div>
        </div>

        {/* Jobs Progress Table */}
        <div className="lg:col-span-2 bg-surface rounded-xl border border-border p-5 space-y-4">
          <h2 className="text-xs font-mono uppercase tracking-wider font-semibold text-slate-300 pb-2 border-b border-border flex items-center justify-between">
            <span>Active & Historic Bulk Ingestion Jobs</span>
            <span className="text-[11px] font-mono text-slate-500">Live Progress Stream</span>
          </h2>

          <div className="space-y-3">
            {jobs.map((j) => (
              <div key={j.jobId} className="p-4 rounded-lg bg-slate-900 border border-border space-y-3">
                <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-2">
                  <div>
                    <div className="flex items-center gap-2">
                      <span className="font-semibold text-slate-200 text-sm">{j.fileName}</span>
                      <span className="text-[10px] font-mono px-2 py-0.5 rounded bg-slate-800 text-slate-400">
                        {j.jobId}
                      </span>
                    </div>
                    <div className="text-[11px] font-mono text-slate-500 mt-0.5 flex items-center gap-2">
                      <Clock className="w-3 h-3" />
                      Started: {new Date(j.startedAtUtc).toLocaleTimeString()}
                    </div>
                  </div>

                  <div className="flex items-center gap-3">
                    {j.status === 'COMPLETED' ? (
                      <span className="flex items-center gap-1.5 text-xs font-mono font-semibold text-accent bg-accent/10 px-2.5 py-1 rounded border border-accent/20">
                        <CheckCircle className="w-3.5 h-3.5" /> COMPLETED
                      </span>
                    ) : (
                      <span className="flex items-center gap-1.5 text-xs font-mono font-semibold text-primary bg-primary/10 px-2.5 py-1 rounded border border-primary/20">
                        <RefreshCw className="w-3.5 h-3.5 animate-spin" /> {j.percentComplete}% RUNNING
                      </span>
                    )}
                  </div>
                </div>

                {/* Progress Bar */}
                <div className="space-y-1">
                  <div className="w-full bg-slate-950 rounded-full h-2 overflow-hidden border border-border">
                    <div
                      className={`h-full rounded-full transition-all duration-500 ${
                        j.status === 'COMPLETED' ? 'bg-accent' : 'bg-primary'
                      }`}
                      style={{ width: `${j.percentComplete}%` }}
                    />
                  </div>

                  <div className="flex justify-between items-center text-[11px] font-mono text-slate-400 pt-1">
                    <span>
                      Processed: <strong className="text-slate-200">{j.processedRecords.toLocaleString()}</strong> / {j.totalRecords.toLocaleString()}
                    </span>
                    <span className="flex items-center gap-1 text-critical font-bold">
                      <AlertTriangle className="w-3 h-3" />
                      Hits Flagged: {j.hitCount}
                    </span>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
};
