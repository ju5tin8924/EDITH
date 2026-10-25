import React, { useState, useEffect } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import QRCode from 'qrcode';
import { 
  KeyRound, 
  CheckCircle2, 
  Copy, 
  Check, 
  Download, 
  ArrowRight,
  Smartphone
} from 'lucide-react';
import { EdithApi, setSession } from '../services/api';
import { EdithLoader } from './EdithLogo';
import { GeneralErrorBanner } from './ErrorEngineUi';

export const OnboardingView: React.FC = () => {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const tokenParam = searchParams.get('token') || '';

  // Step management
  const [currentStep, setCurrentStep] = useState<1 | 2 | 3>(1);
  const [token, setToken] = useState(tokenParam);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // User & Tenant info from token validation
  const [userInfo, setUserInfo] = useState<{
    username: string;
    fullName: string;
    role: string;
    tenantName: string;
  } | null>(null);

  // Step 1: Password inputs
  const [tempPassword, setTempPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');

  // Step 2: 2FA state
  const [mfaSecret, setMfaSecret] = useState('');
  const [otpAuthUri, setOtpAuthUri] = useState('');
  const [qrCodeDataUrl, setQrCodeDataUrl] = useState('');
  const [totpCode, setTotpCode] = useState('');
  const [secretCopied, setSecretCopied] = useState(false);

  // Generate real scannable QR Code whenever otpAuthUri or mfaSecret is loaded
  useEffect(() => {
    const uri = otpAuthUri || (mfaSecret && userInfo?.username
      ? `otpauth://totp/EDITH-AML:${encodeURIComponent(userInfo.username)}?secret=${mfaSecret}&issuer=EDITH-AML&algorithm=SHA1&digits=6&period=30`
      : '');

    if (uri) {
      QRCode.toDataURL(uri, {
        width: 200,
        margin: 1,
        color: {
          dark: '#000000',
          light: '#ffffff'
        },
        errorCorrectionLevel: 'M'
      })
      .then(url => setQrCodeDataUrl(url))
      .catch(err => {
        console.error('Failed to generate TOTP QR code', err);
      });
    }
  }, [otpAuthUri, mfaSecret, userInfo?.username]);

  // Step 3: Recovery codes
  const [recoveryCodes, setRecoveryCodes] = useState<string[]>([]);
  const [codesSaved, setCodesSaved] = useState(false);
  const [codesCopied, setCodesCopied] = useState(false);

  useEffect(() => {
    if (token) {
      validateToken(token);
    } else {
      setLoading(false);
    }
  }, [token]);

  const validateToken = async (invitationToken: string) => {
    setLoading(true);
    setError(null);
    try {
      const res = await EdithApi.validateOnboardingToken(invitationToken);
      if (res && res.valid) {
        setUserInfo({
          username: res.username,
          fullName: res.fullName,
          role: res.role,
          tenantName: res.tenantName
        });
        // Pre-fetch MFA secret
        const mfaRes = await EdithApi.getMfaSetup(invitationToken);
        if (mfaRes && mfaRes.secretBase32) {
          setMfaSecret(mfaRes.secretBase32);
          setOtpAuthUri(mfaRes.otpAuthUri);
        }
      } else {
        setError(res?.error || 'Invitation link is invalid or has expired. Please request a new invitation.');
      }
    } catch {
      setError('Unable to verify your invitation right now. Please check your connection and try again.');
    } finally {
      setLoading(false);
    }
  };

  const handlePasswordSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!tempPassword.trim()) {
      setError('Please provide your temporary password from your invitation email.');
      return;
    }
    if (newPassword.length < 8) {
      setError('New password must be at least 8 characters long.');
      return;
    }
    if (newPassword !== confirmPassword) {
      setError('New password and confirmation do not match.');
      return;
    }
    setError(null);
    setCurrentStep(2);
  };

  const handle2FaSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (totpCode.trim().length !== 6) {
      setError('Please enter the full 6-digit code from your authenticator app.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const res = await EdithApi.completeOnboarding({
        token,
        temporaryPassword: tempPassword.trim(),
        newPassword: newPassword.trim(),
        totpCode: totpCode.trim(),
        mfaSecret
      });

      if (res && res.success) {
        setRecoveryCodes(res.recoveryCodes || []);
        if (res.accessToken && userInfo) {
          setSession({
            token: res.accessToken,
            expiresAtUtc: new Date(Date.now() + 14400000).toISOString(),
            username: userInfo.username,
            role: userInfo.role as any,
            tenantId: '11111111-1111-1111-1111-111111111111'
          });
        }
        setCurrentStep(3);
      } else {
        setError(res?.message || 'Verification failed. Please check your 6-digit authenticator code and temporary password.');
      }
    } catch {
      setError('Unable to complete account setup right now. Please check your connection and try again.');
    } finally {
      setLoading(false);
    }
  };

  const copySecret = () => {
    navigator.clipboard.writeText(mfaSecret);
    setSecretCopied(true);
    setTimeout(() => setSecretCopied(false), 2000);
  };

  const copyRecoveryCodes = () => {
    navigator.clipboard.writeText(recoveryCodes.join('\n'));
    setCodesCopied(true);
    setTimeout(() => setCodesCopied(false), 2000);
  };

  const downloadRecoveryCodes = () => {
    const text = `EDITH AML PLATFORM — EMERGENCY RECOVERY CODES\nAccount: ${userInfo?.username}\nGenerated: ${new Date().toISOString()}\n\nEach code can be used once if you lose access to your authenticator app:\n\n${recoveryCodes.map((c, i) => `${i + 1}. ${c}`).join('\n')}\n\nKeep this file in a secure, encrypted location.`;
    const blob = new Blob([text], { type: 'text/plain' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `edith-recovery-codes-${userInfo?.username || 'user'}.txt`;
    a.click();
    URL.revokeObjectURL(url);
  };

  const handleFinish = () => {
    navigate('/dashboard');
  };

  return (
    <div className="min-h-screen bg-[#f8fafc] text-ink flex flex-col justify-center items-center p-4">
      {/* Container */}
      <div className="w-full max-w-xl bg-white border border-border rounded-2xl shadow-lg overflow-hidden">
        {/* Header Banner */}
        <div className="bg-[#0f172a] text-white p-6 border-b border-border">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl bg-primary flex items-center justify-center font-mono font-bold text-lg shadow-sm">
                E
              </div>
              <div>
                <h1 className="text-base font-bold tracking-tight">EDITH Compliance Gateway</h1>
                <p className="text-xs font-mono text-slate-400">Security Activation & Mandatory 2FA Setup</p>
              </div>
            </div>
            <span className="px-2.5 py-1 rounded text-[10px] font-mono font-bold bg-primary/20 text-blue-300 border border-blue-500/30">
              STAGE {currentStep} OF 3
            </span>
          </div>

          {/* Stepper Dots */}
          <div className="flex items-center gap-2 mt-6">
            <div className={`flex-1 h-1.5 rounded-full ${currentStep >= 1 ? 'bg-primary' : 'bg-slate-700'}`} />
            <div className={`flex-1 h-1.5 rounded-full ${currentStep >= 2 ? 'bg-primary' : 'bg-slate-700'}`} />
            <div className={`flex-1 h-1.5 rounded-full ${currentStep >= 3 ? 'bg-accent' : 'bg-slate-700'}`} />
          </div>
        </div>

        {/* Content Body */}
        <div className="p-6 sm:p-8 space-y-6">
          {loading && userInfo && (
            <EdithLoader
              overlay
              size="md"
              label="ACTIVATING ACCOUNT..."
              sublabel="Completing security setup, please wait"
            />
          )}

          {loading && !userInfo && (
            <div className="py-8 flex items-center justify-center">
              <EdithLoader
                size="md"
                label="VERIFYING INVITATION..."
                sublabel="Checking your account setup details"
              />
            </div>
          )}

          {error && (
            <GeneralErrorBanner
              error={error}
              category="AUTH"
              onRetry={!userInfo && token ? () => validateToken(token) : undefined}
              onDismiss={() => setError(null)}
            />
          )}

          {!userInfo && !loading && (
            <div className="space-y-4">
              <p className="text-xs text-muted">
                Please enter the onboarding token provided in your welcome email to continue.
              </p>
              <div className="flex gap-2">
                <input
                  type="text"
                  placeholder="Paste invitation token here..."
                  value={token}
                  onChange={e => setToken(e.target.value)}
                  className="flex-1 px-3 py-2 text-xs font-mono border border-border rounded-lg bg-surface text-ink"
                />
                <button
                  type="button"
                  onClick={() => validateToken(token)}
                  className="px-4 py-2 bg-primary text-white text-xs font-bold rounded-lg hover:bg-primary/90"
                >
                  Verify
                </button>
              </div>
            </div>
          )}

          {userInfo && (
            <>
              {/* User Identity Chip */}
              <div className="p-3 bg-surface rounded-xl border border-border flex items-center justify-between text-xs">
                <div>
                  <div className="font-bold text-ink">{userInfo.fullName}</div>
                  <div className="text-[11px] font-mono text-muted">{userInfo.username}</div>
                </div>
                <div className="text-right">
                  <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-primary/10 text-primary border border-primary/20">
                    {userInfo.role}
                  </span>
                  <div className="text-[10px] font-mono text-muted mt-0.5">{userInfo.tenantName}</div>
                </div>
              </div>

              {/* STEP 1: CHANGE TEMPORARY PASSWORD */}
              {currentStep === 1 && (
                <form onSubmit={handlePasswordSubmit} className="space-y-4">
                  <div>
                    <h2 className="text-sm font-bold text-ink flex items-center gap-2">
                      <KeyRound className="w-4 h-4 text-primary" />
                      Step 1: Rotate Temporary Password
                    </h2>
                    <p className="text-xs text-muted mt-1">
                      Enter the auto-generated password from your onboarding email, then choose a new secure password.
                    </p>
                  </div>

                  <div className="space-y-3">
                    <div>
                      <label className="block text-[11px] font-bold text-muted uppercase tracking-wider mb-1">
                        Temporary Password (From Email)
                      </label>
                      <input
                        type="password"
                        required
                        value={tempPassword}
                        onChange={e => setTempPassword(e.target.value)}
                        placeholder="••••••••••••••••"
                        className="w-full px-3 py-2 text-xs font-mono border border-border rounded-lg bg-surface text-ink focus:outline-none focus:border-primary"
                      />
                    </div>

                    <div>
                      <label className="block text-[11px] font-bold text-muted uppercase tracking-wider mb-1">
                        New Permanent Password
                      </label>
                      <input
                        type="password"
                        required
                        value={newPassword}
                        onChange={e => setNewPassword(e.target.value)}
                        placeholder="Min 8 characters, letters & symbols"
                        className="w-full px-3 py-2 text-xs font-mono border border-border rounded-lg bg-surface text-ink focus:outline-none focus:border-primary"
                      />
                    </div>

                    <div>
                      <label className="block text-[11px] font-bold text-muted uppercase tracking-wider mb-1">
                        Confirm New Password
                      </label>
                      <input
                        type="password"
                        required
                        value={confirmPassword}
                        onChange={e => setConfirmPassword(e.target.value)}
                        placeholder="Repeat new password"
                        className="w-full px-3 py-2 text-xs font-mono border border-border rounded-lg bg-surface text-ink focus:outline-none focus:border-primary"
                      />
                    </div>
                  </div>

                  <button
                    type="submit"
                    className="w-full py-2.5 bg-primary text-white font-bold text-xs rounded-xl hover:bg-primary/90 transition-all flex items-center justify-center gap-2 shadow-xs"
                  >
                    <span>Proceed to 2FA Setup</span>
                    <ArrowRight className="w-3.5 h-3.5" />
                  </button>
                </form>
              )}

              {/* STEP 2: 2FA SETUP (TOTP) */}
              {currentStep === 2 && (
                <form onSubmit={handle2FaSubmit} className="space-y-5">
                  <div>
                    <h2 className="text-sm font-bold text-ink flex items-center gap-2">
                      <Smartphone className="w-4 h-4 text-primary" />
                      Step 2: Configure Authenticator App (2FA)
                    </h2>
                    <p className="text-xs text-muted mt-1">
                      Scan the QR code with <strong>Google Authenticator</strong>, <strong>Microsoft Authenticator</strong>, or enter the secret key manually.
                    </p>
                  </div>

                  <div className="p-4 bg-surface rounded-xl border border-border flex flex-col sm:flex-row items-center gap-4">
                    {/* Genuine Scannable 2FA QR Code */}
                    <div className="w-36 h-36 bg-white border border-border rounded-xl flex flex-col items-center justify-center p-2 shadow-xs shrink-0">
                      {qrCodeDataUrl ? (
                        <img 
                          src={qrCodeDataUrl} 
                          alt="Authenticator 2FA Setup QR Code" 
                          className="w-28 h-28 block object-contain select-none"
                        />
                      ) : (
                        <div className="flex flex-col items-center justify-center gap-1.5 py-4">
                          <EdithLoader size="sm" />
                          <span className="text-[10px] font-mono text-muted">Rendering QR...</span>
                        </div>
                      )}
                      <span className="text-[9px] font-mono font-bold text-muted mt-0.5">RFC 6238 TOTP</span>
                    </div>

                    <div className="space-y-2 min-w-0 flex-1">
                      <div className="text-[11px] font-bold text-muted uppercase tracking-wider">
                        Manual Entry Secret Key:
                      </div>
                      <div className="flex items-center gap-2 bg-white px-3 py-2 rounded-lg border border-border">
                        <code className="text-xs font-mono font-bold text-primary select-all truncate">
                          {mfaSecret || 'JBSWY3DPEHPK3PXP'}
                        </code>
                        <button
                          type="button"
                          onClick={copySecret}
                          className="p-1 hover:bg-surface rounded text-muted hover:text-ink transition-colors shrink-0"
                          title="Copy Key"
                        >
                          {secretCopied ? <Check className="w-3.5 h-3.5 text-accent" /> : <Copy className="w-3.5 h-3.5" />}
                        </button>
                      </div>
                      <p className="text-[10px] text-muted">
                        Algorithm: SHA1 • Period: 30s • Digits: 6
                      </p>
                      {otpAuthUri && (
                        <a 
                          href={otpAuthUri} 
                          className="text-[10px] text-primary hover:underline block truncate font-mono"
                          title="Open in Authenticator App"
                        >
                          Launch in Authenticator App
                        </a>
                      )}
                    </div>
                  </div>

                  <div>
                    <label className="block text-[11px] font-bold text-muted uppercase tracking-wider mb-1">
                      Enter 6-Digit Authenticator Verification Code
                    </label>
                    <input
                      type="text"
                      maxLength={6}
                      required
                      value={totpCode}
                      onChange={e => setTotpCode(e.target.value.replace(/\D/g, ''))}
                      placeholder="e.g. 849201"
                      className="w-full text-center text-xl font-mono tracking-widest px-4 py-2.5 border-2 border-primary/40 rounded-xl bg-surface text-ink focus:outline-none focus:border-primary font-bold"
                    />
                  </div>

                  <div className="flex gap-2">
                    <button
                      type="button"
                      onClick={() => setCurrentStep(1)}
                      className="px-4 py-2.5 border border-border text-ink text-xs font-bold rounded-xl hover:bg-surface"
                    >
                      Back
                    </button>
                    <button
                      type="submit"
                      disabled={loading || totpCode.length !== 6}
                      className="flex-1 py-2.5 bg-primary text-white font-bold text-xs rounded-xl hover:bg-primary/90 transition-all disabled:opacity-50 flex items-center justify-center gap-2 shadow-xs"
                    >
                      {loading ? 'Verifying...' : 'Verify Code & Enable 2FA'}
                    </button>
                  </div>
                </form>
              )}

              {/* STEP 3: RECOVERY CODES */}
              {currentStep === 3 && (
                <div className="space-y-5">
                  <div>
                    <h2 className="text-sm font-bold text-accent flex items-center gap-2">
                      <CheckCircle2 className="w-4 h-4 text-accent" />
                      Step 3: Save Emergency Backup Recovery Codes
                    </h2>
                    <p className="text-xs text-muted mt-1">
                      Your account is activated and 2FA is active. Save these 8 one-time backup codes in case you lose access to your authenticator phone.
                    </p>
                  </div>

                  {/* 2-Column Code Matrix */}
                  <div className="p-4 bg-surface rounded-xl border border-border">
                    <div className="grid grid-cols-2 gap-2">
                      {recoveryCodes.map((code, idx) => (
                        <div
                          key={idx}
                          className="px-3 py-2 bg-white rounded-lg border border-border text-xs font-mono font-bold text-ink flex items-center justify-between"
                        >
                          <span className="text-[10px] text-muted">{idx + 1}.</span>
                          <span>{code}</span>
                        </div>
                      ))}
                    </div>

                    <div className="flex gap-2 mt-3 pt-3 border-t border-border">
                      <button
                        type="button"
                        onClick={copyRecoveryCodes}
                        className="flex-1 py-2 px-3 bg-white border border-border text-ink text-xs font-bold rounded-lg hover:bg-surface transition-all flex items-center justify-center gap-2"
                      >
                        {codesCopied ? <Check className="w-3.5 h-3.5 text-accent" /> : <Copy className="w-3.5 h-3.5" />}
                        <span>{codesCopied ? 'Copied' : 'Copy All'}</span>
                      </button>
                      <button
                        type="button"
                        onClick={downloadRecoveryCodes}
                        className="flex-1 py-2 px-3 bg-white border border-border text-ink text-xs font-bold rounded-lg hover:bg-surface transition-all flex items-center justify-center gap-2"
                      >
                        <Download className="w-3.5 h-3.5" />
                        <span>Download (.txt)</span>
                      </button>
                    </div>
                  </div>

                  <label className="flex items-center gap-2.5 text-xs text-ink cursor-pointer select-none">
                    <input
                      type="checkbox"
                      checked={codesSaved}
                      onChange={e => setCodesSaved(e.target.checked)}
                      className="rounded border-border text-primary focus:ring-0 w-4 h-4"
                    />
                    <span>I have copied or downloaded my emergency recovery codes</span>
                  </label>

                  <button
                    type="button"
                    disabled={!codesSaved}
                    onClick={handleFinish}
                    className="w-full py-2.5 bg-accent text-white font-bold text-xs rounded-xl hover:bg-accent/90 transition-all disabled:opacity-50 flex items-center justify-center gap-2 shadow-xs"
                  >
                    <span>Launch EDITH Compliance Workspace</span>
                    <ArrowRight className="w-3.5 h-3.5" />
                  </button>
                </div>
              )}
            </>
          )}
        </div>
      </div>
    </div>
  );
};
