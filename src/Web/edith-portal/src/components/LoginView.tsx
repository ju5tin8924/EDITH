import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { 
  Eye, 
  EyeOff, 
  ArrowRight, 
  ArrowLeft, 
  CheckCircle2, 
  Smartphone, 
  Key, 
  Clock, 
  Sparkles,
  ExternalLink
} from 'lucide-react';
import { EdithApi, setSession } from '../services/api';
import { UserRole } from '../types';
import { EdithLogo, EdithLoader } from './EdithLogo';
import { GeneralErrorBanner } from './ErrorEngineUi';

interface LoginViewProps {
  onLoginSuccess?: () => void;
}

export const LoginView: React.FC<LoginViewProps> = ({ onLoginSuccess }) => {
  const navigate = useNavigate();

  // Authentication State
  const [step, setStep] = useState<1 | 2>(1);
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Onboarding Required State
  const [onboardingToken, setOnboardingToken] = useState<string | null>(null);

  // Step 2: MFA Challenge State
  const [mfaSessionToken, setMfaSessionToken] = useState<string>('');
  const [userRole, setUserRole] = useState<UserRole>('ADMIN');
  const [totpCode, setTotpCode] = useState<string>('');
  const [useRecoveryCode, setUseRecoveryCode] = useState<boolean>(false);
  const [countdown, setCountdown] = useState<number>(300); // 5 minutes

  // Countdown timer for MFA session
  useEffect(() => {
    if (step !== 2) return;
    const interval = setInterval(() => {
      setCountdown(prev => {
        if (prev <= 1) {
          clearInterval(interval);
          setError('MFA session expired. Please re-enter your credentials.');
          setStep(1);
          return 0;
        }
        return prev - 1;
      });
    }, 1000);
    return () => clearInterval(interval);
  }, [step]);

  // Step 1: Submit Credentials
  const handleCredentialsSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!username.trim() || !password) {
      setError('Please provide your username / email and password.');
      return;
    }

    setLoading(true);
    setError(null);
    setOnboardingToken(null);

    try {
      const res = await EdithApi.login({ username: username.trim(), password });

      if (res.status === 'ONBOARDING_REQUIRED') {
        // User created by Admin with auto-generated password
        setOnboardingToken(res.onboardingToken);
        setError(null);
      } else if (res.status === 'MFA_REQUIRED') {
        // Password valid, 2FA challenge triggered
        setMfaSessionToken(res.mfaSessionToken);
        setUserRole(res.role || 'ADMIN');
        setStep(2);
        setTotpCode('');
        setCountdown(300);
        setError(null);
      } else if (res.status === 'SUCCESS' && res.accessToken) {
        // Direct issuance (if MFA bypassed/completed)
        completeLogin(res.accessToken, res.role || 'ADMIN', res.tenantId);
      } else {
        setError(res.message || 'Invalid username or password. Please verify your credentials.');
      }
    } catch {
      setError('Unable to complete sign-in right now. Please check your connection and try again.');
    } finally {
      setLoading(false);
    }
  };

  // Step 2: Submit 2FA / Recovery Code
  const handleMfaSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const cleanCode = totpCode.trim().replace(/\s+/g, '');
    if (!cleanCode) {
      setError(useRecoveryCode ? 'Please enter your recovery code.' : 'Please enter the 6-digit authenticator code.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const res = await EdithApi.verifyMfa({
        mfaSessionToken,
        code: cleanCode
      });

      if (res.accessToken) {
        completeLogin(res.accessToken, res.role || userRole, res.tenantId);
      } else {
        setError(res.message || 'Invalid verification code. Please check your authenticator and try again.');
      }
    } catch {
      setError('Verification could not be completed right now. Please check your connection and try again.');
    } finally {
      setLoading(false);
    }
  };

  const completeLogin = (token: string, role: UserRole, tenantId?: string) => {
    setSession({
      token,
      expiresAtUtc: new Date(Date.now() + 86400000).toISOString(),
      username,
      role,
      tenantId: tenantId || '11111111-1111-1111-1111-111111111111'
    });

    if (onLoginSuccess) {
      onLoginSuccess();
    }
    navigate('/dashboard');
  };

  const formatCountdown = (seconds: number) => {
    const mins = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return `${mins}:${secs < 10 ? '0' : ''}${secs}`;
  };

  return (
    <div className="min-h-screen w-screen bg-[#070b14] text-slate-100 flex items-center justify-center p-4 relative overflow-hidden font-sans select-none">
      {/* Background Architectural Grid & Subtle Radial Glow */}
      <div 
        className="absolute inset-0 opacity-20 pointer-events-none"
        style={{
          backgroundImage: 'radial-gradient(circle at 50% 50%, #3b82f6 1px, transparent 1px)',
          backgroundSize: '36px 36px'
        }}
      />
      <div className="absolute top-1/4 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[600px] h-[400px] bg-primary/15 blur-[120px] rounded-full pointer-events-none" />

      {/* Main Authentication Card */}
      <div className="relative w-full max-w-md bg-[#0d1527]/95 border border-slate-800 rounded-2xl shadow-2xl backdrop-blur-xl p-6 sm:p-8 space-y-6 z-10">
        {/* Header */}
        <div className="text-center space-y-2">
          <div className="flex flex-col items-center justify-center gap-2 pt-1">
            <div className="w-16 h-16 rounded-2xl bg-[#121858]/90 border border-primary/40 flex items-center justify-center shadow-lg p-1.5">
              <EdithLogo variant="dark" animated className="w-14 h-14" />
            </div>
            <h1 className="text-2xl font-black tracking-wider text-white font-mono">
              EDITH
            </h1>
          </div>
          <p className="text-xs text-slate-400">
            Sign in to your account
          </p>
        </div>

        {/* Global Error Banner */}
        {loading && (
          <EdithLoader
            overlay
            variant="dark"
            size="md"
            label={step === 1 ? 'VERIFYING CREDENTIALS...' : 'VERIFYING SECURITY CODE...'}
            sublabel="Establishing secure session, please wait"
          />
        )}

        {error && (
          <GeneralErrorBanner
            error={error}
            category="AUTH"
            dark
            onDismiss={() => setError(null)}
          />
        )}

        {/* Onboarding Required Notice Banner */}
        {onboardingToken && (
          <div className="p-4 rounded-xl bg-warning/15 border border-warning/40 text-warning text-xs font-mono space-y-3 animate-in fade-in duration-200">
            <div className="flex items-start gap-2">
              <Sparkles className="w-4 h-4 shrink-0 text-warning mt-0.5" />
              <div>
                <div className="font-bold text-white text-[12px]">Mandatory Account Onboarding</div>
                <div className="text-[11px] text-warning/90 mt-0.5">
                  Your officer credentials have been provisioned with a temporary password. You must set a permanent password and enroll your Two-Factor Authenticator to activate access.
                </div>
              </div>
            </div>

            <button
              type="button"
              onClick={() => navigate(`/onboarding?token=${encodeURIComponent(onboardingToken)}`)}
              className="w-full py-2.5 px-3 rounded-lg bg-warning text-slate-950 font-bold text-xs flex items-center justify-center gap-2 hover:bg-warning/90 transition shadow-md cursor-pointer"
            >
              <span>Complete 2FA Setup &amp; Password Rotation</span>
              <ExternalLink className="w-3.5 h-3.5" />
            </button>
          </div>
        )}

        {/* STEP 1: Primary Credentials Form */}
        {step === 1 && !onboardingToken && (
          <form onSubmit={handleCredentialsSubmit} className="space-y-4">
            {/* Username / Email Input */}
            <div className="space-y-1">
              <label className="text-[11px] font-mono uppercase tracking-wider text-slate-300 font-bold">
                Username / Identity ID
              </label>
              <div className="relative">
                <input
                  type="text"
                  required
                  autoComplete="username"
                  value={username}
                  onChange={e => setUsername(e.target.value)}
                  placeholder="name@organization.com"
                  className="w-full px-3 py-2.5 rounded-xl bg-slate-900/90 border border-slate-700 text-white font-mono text-xs placeholder:text-slate-500 focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary transition"
                />
              </div>
            </div>

            {/* Password Input */}
            <div className="space-y-1">
              <label className="text-[11px] font-mono uppercase tracking-wider text-slate-300 font-bold">
                Password
              </label>
              <div className="relative">
                <input
                  type={showPassword ? 'text' : 'password'}
                  required
                  autoComplete="current-password"
                  value={password}
                  onChange={e => setPassword(e.target.value)}
                  placeholder="••••••••••••"
                  className="w-full pl-3 pr-10 py-2.5 rounded-xl bg-slate-900/90 border border-slate-700 text-white font-mono text-xs placeholder:text-slate-500 focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary transition"
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(!showPassword)}
                  className="absolute right-3 top-2.5 text-slate-400 hover:text-slate-200 transition cursor-pointer"
                >
                  {showPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                </button>
              </div>
            </div>

            {/* Submit Button */}
            <button
              type="submit"
              disabled={loading}
              className="w-full py-3 px-4 rounded-xl bg-primary hover:bg-primary-hover text-white font-mono font-bold text-xs flex items-center justify-center gap-2 shadow-lg shadow-primary/20 transition cursor-pointer disabled:opacity-50"
            >
              {loading ? (
                <>
                  <div className="w-4 h-4 border-2 border-white/20 border-t-white rounded-full animate-spin" />
                  <span>Validating Credentials...</span>
                </>
              ) : (
                <>
                  <span>Authenticate &amp; Proceed</span>
                  <ArrowRight className="w-4 h-4" />
                </>
              )}
            </button>
          </form>
        )}

        {/* STEP 2: 2FA TOTP Verification Challenge */}
        {step === 2 && (
          <form onSubmit={handleMfaSubmit} className="space-y-5 animate-in fade-in duration-200">
            {/* Header with back button */}
            <div className="flex items-center justify-between border-b border-slate-800 pb-3">
              <button
                type="button"
                onClick={() => { setStep(1); setError(null); }}
                className="text-[11px] font-mono text-slate-400 hover:text-white flex items-center gap-1 transition cursor-pointer"
              >
                <ArrowLeft className="w-3.5 h-3.5" />
                <span>Back</span>
              </button>

              <div className="flex items-center gap-1 text-[11px] font-mono text-accent">
                <Clock className="w-3.5 h-3.5" />
                <span>{formatCountdown(countdown)}</span>
              </div>
            </div>

            {/* User Details Badge */}
            <div className="p-3 rounded-xl bg-slate-900/80 border border-slate-800 flex items-center justify-between">
              <div className="min-w-0">
                <div className="text-[10px] font-mono text-slate-400 uppercase">Authenticated Identity</div>
                <div className="text-xs font-bold font-mono text-white truncate">{username}</div>
              </div>
              <span className="px-2 py-0.5 rounded bg-primary/20 text-primary-light text-[10px] font-mono font-bold border border-primary/30">
                {userRole}
              </span>
            </div>

            {/* Verification Mode Header */}
            {!useRecoveryCode ? (
              <div className="space-y-3">
                <div className="flex items-center gap-2 text-white font-mono text-xs font-bold">
                  <Smartphone className="w-4 h-4 text-primary-light" />
                  <span>Two-Factor Authenticator Code</span>
                </div>
                <p className="text-[11px] text-slate-400 leading-relaxed font-mono">
                  Enter the 6-digit code displayed in Google Authenticator, Microsoft Authenticator, or 1Password.
                </p>

                <div className="space-y-1">
                  <input
                    type="text"
                    autoFocus
                    required
                    maxLength={6}
                    value={totpCode}
                    onChange={e => {
                      const val = e.target.value.replace(/\D/g, '');
                      setTotpCode(val);
                      if (val.length === 6) {
                        // Optional auto-submit when 6 digits entered
                      }
                    }}
                    placeholder="000000"
                    className="w-full text-center tracking-[0.5em] font-mono font-bold text-2xl py-3 rounded-xl bg-slate-950 border border-slate-700 text-primary-light placeholder:text-slate-700 focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary transition"
                  />
                </div>
              </div>
            ) : (
              <div className="space-y-3">
                <div className="flex items-center gap-2 text-white font-mono text-xs font-bold">
                  <Key className="w-4 h-4 text-warning" />
                  <span>Emergency Recovery Code</span>
                </div>
                <p className="text-[11px] text-slate-400 leading-relaxed font-mono">
                  Enter one of your 8 single-use emergency recovery codes issued during account onboarding.
                </p>

                <input
                  type="text"
                  autoFocus
                  required
                  value={totpCode}
                  onChange={e => setTotpCode(e.target.value.toUpperCase())}
                  placeholder="A1B2-C3D4"
                  className="w-full text-center uppercase tracking-widest font-mono font-bold text-lg py-3 rounded-xl bg-slate-950 border border-slate-700 text-warning placeholder:text-slate-700 focus:outline-none focus:border-warning focus:ring-1 focus:ring-warning transition"
                />
              </div>
            )}

            {/* Toggle Recovery Code mode */}
            <div className="text-center pt-1">
              <button
                type="button"
                onClick={() => {
                  setUseRecoveryCode(!useRecoveryCode);
                  setTotpCode('');
                  setError(null);
                }}
                className="text-[11px] font-mono text-slate-400 hover:text-primary-light transition underline underline-offset-4 cursor-pointer"
              >
                {useRecoveryCode 
                  ? '← Use 6-Digit Authenticator App Code' 
                  : 'Lost authenticator? Use single-use recovery code'}
              </button>
            </div>

            {/* Submit Verification Button */}
            <button
              type="submit"
              disabled={loading || totpCode.trim().length === 0}
              className="w-full py-3 px-4 rounded-xl bg-accent hover:bg-accent-hover text-slate-950 font-mono font-bold text-xs flex items-center justify-center gap-2 shadow-lg shadow-accent/20 transition cursor-pointer disabled:opacity-50"
            >
              {loading ? (
                <>
                  <div className="w-4 h-4 border-2 border-slate-950/20 border-t-slate-950 rounded-full animate-spin" />
                  <span>Verifying Code...</span>
                </>
              ) : (
                <>
                  <CheckCircle2 className="w-4 h-4" />
                  <span>Verify &amp; Sign In</span>
                </>
              )}
            </button>
          </form>
        )}
      </div>
    </div>
  );
};
export default LoginView;
