import React, { useEffect, useState } from 'react';
import styled from 'styled-components';
import {
  Eye,
  EyeOff,
  Info,
  KeyRound,
  Lock,
  LogIn,
  Mail,
  Shield,
  Sun,
  Moon,
  Target,
  Users,
  Eye as EyeIcon,
  BarChart3,
  Flag,
  UserPlus,
} from 'lucide-react';
import { api } from '../services/api';
import { BrandMark } from '../components/BrandMark';
import { useThemeMode } from '../styles/ThemeMode';

const Page = styled.div`
  display: flex;
  flex-direction: column;
  min-height: 100vh;
  padding: 4px 9px;
  background:
    radial-gradient(circle at 8% 8%, rgba(124, 58, 237, 0.08), transparent 28%),
    radial-gradient(circle at 92% 82%, rgba(249, 115, 22, 0.06), transparent 24%),
    ${({ theme }) => theme.color.bg};
  color: ${({ theme }) => theme.color.text};

  @media (max-width: 960px) { padding: 0; }
`;

const Shell = styled.div`
  flex: 1;
  display: grid;
  grid-template-columns: minmax(0, 1.42fr) minmax(430px, 1fr);
  min-height: 0;
  width: 100%;
  max-width: none;
  margin: 0 auto;
  overflow: hidden;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-bottom: 0;
  border-radius: 22px 22px 0 0;
  background: ${({ theme }) => theme.color.surface};
  box-shadow: ${({ theme }) => theme.shadow.lg};

  @media (max-width: 960px) {
    grid-template-columns: 1fr;
    border: 0;
    border-radius: 0;
    box-shadow: none;
  }
`;

const BrandPanel = styled.section`
  position: relative;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  gap: 48px;
  padding: 28px clamp(24px, 3vw, 50px) 76px;
  overflow: hidden;
  background:
    radial-gradient(ellipse 62% 48% at 50% 38%, rgba(79, 70, 229, 0.09), transparent 65%),
    radial-gradient(ellipse 55% 42% at 82% 88%, rgba(249, 115, 22, 0.06), transparent 58%),
    ${({ theme }) => theme.color.surface};
  border-right: 1px solid ${({ theme }) => theme.color.border};

  @media (max-width: 960px) {
    display: none;
  }

  @media (min-width: 961px) and (min-height: 850px) {
    padding-bottom: 112px;
  }

  &::before {
    content: '';
    position: absolute;
    inset: 8% 8% 14% 4%;
    height: auto;
    background:
      linear-gradient(122deg, transparent 49.6%, rgba(124, 58, 237, 0.11) 50%, transparent 50.4%),
      linear-gradient(58deg, transparent 49.6%, rgba(37, 99, 235, 0.08) 50%, transparent 50.4%);
    background-size: 210px 210px;
    opacity: 0.16;
    pointer-events: none;
    mask-image: radial-gradient(ellipse at center, black 30%, transparent 75%);
  }
`;

const SpectrumWaves = styled.svg`
  position: absolute;
  z-index: 0;
  right: -4%;
  bottom: -16px;
  width: 112%;
  height: 160px;
  opacity: 0.34;
  pointer-events: none;
`;

const BrandTop = styled.div`
  position: relative;
  z-index: 1;
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 13px;
  font-weight: 700;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  color: ${({ theme }) => theme.color.textMuted};
`;

const Hero = styled.div`
  position: relative;
  z-index: 1;
  display: grid;
  justify-items: center;
  align-self: center;
  gap: 8px;
  width: min(100%, 620px);
  text-align: center;
`;

const GemWrap = styled.div`
  margin-bottom: -4px;
  transform: translateY(4px);
  filter: drop-shadow(0 22px 26px rgba(79, 70, 229, 0.23));
  img { display: block; width: 194px; height: auto; }
`;

const ProductName = styled.h1`
  margin: 0;
  font-family: ${({ theme }) => theme.font.display};
  font-size: clamp(52px, 5.05vw, 82px);
  font-weight: 300;
  letter-spacing: 0.21em;
  padding-left: 0.21em;
  line-height: 1;
  color: ${({ theme }) => theme.color.text};

  > span {
    display: block;
    margin-top: 8px;
    font-size: 0.64em;
    font-weight: 400;
    letter-spacing: -0.035em;
    color: #5362f1;
  }
  em { font-style: normal; background: linear-gradient(145deg,#ff9e29,#9650f0 48%,#255fff); background-clip: text; -webkit-background-clip: text; color: transparent; }
`;

const SpectrumRule = styled.span`
  width: min(78%, 350px);
  height: 2px;
  margin: 21px 0 18px;
  border-radius: 999px;
  background: ${({ theme }) => theme.color.gradient};
`;

const Tagline = styled.p`
  margin: 0;
  max-width: 360px;
  font-family: ${({ theme }) => theme.font.body};
  font-size: clamp(25px, 2.05vw, 35px);
  font-weight: 400;
  line-height: 1.3;
  color: ${({ theme }) => theme.color.text};
`;

const Features = styled.ul`
  position: relative;
  z-index: 1;
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: 0;
  margin: 0;
  padding: 0;
  list-style: none;

`;

const Feature = styled.li`
  display: grid;
  position: relative;
  justify-items: center;
  gap: 7px;
  padding: 10px clamp(6px, 1.1vw, 16px);
  text-align: center;

  &:not(:last-child)::after {
    content: '';
    position: absolute;
    top: 12px;
    right: 0;
    width: 1px;
    height: calc(100% - 18px);
    background: ${({ theme }) => theme.color.border};
  }

  svg {
    color: ${({ theme }) => theme.color.brand};
    width: 40px;
    height: 40px;
  }

  strong {
    font-size: 15px;
    font-weight: 800;
  }

  span {
    font-size: 13px;
    line-height: 1.45;
    color: ${({ theme }) => theme.color.textMuted};
  }
`;

const FormPanel = styled.section`
  position: relative;
  display: flex;
  flex-direction: column;
  justify-content: center;
  padding: 110px clamp(28px, 3vw, 52px) 60px;
  background:
    linear-gradient(180deg, color-mix(in srgb, ${({ theme }) => theme.color.surfaceSubtle} 92%, transparent), ${({ theme }) => theme.color.surfaceSubtle});

  @media (max-width: 960px) { padding: 80px 20px 44px; }

  @media (min-width: 961px) and (min-height: 850px) {
    justify-content: flex-start;
    padding-top: 164px;
  }
`;

const ThemeChip = styled.button`
  position: absolute;
  top: 28px;
  right: 30px;
  display: inline-flex;
  align-items: center;
  gap: 8px;
  min-height: 44px;
  padding: 0 12px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.pill};
  background: ${({ theme }) => theme.color.surface};
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 13px;
  font-weight: 600;

  &:hover {
    color: ${({ theme }) => theme.color.text};
    border-color: ${({ theme }) => theme.color.accentBlue};
  }
`;

const FormCard = styled.div`
  width: 100%;
  max-width: 480px;
  margin: 0 auto;
`;

const MobileBrand = styled.div`
  display: none;
  margin-bottom: 28px;
  text-align: center;

  @media (max-width: 960px) {
    display: grid;
    justify-items: center;
    gap: 8px;
  }
`;

const FormTitle = styled.h2`
  margin: 0 0 6px;
  font-size: clamp(27px, 2.2vw, 34px);
  font-weight: 800;
  letter-spacing: -0.02em;
`;

const FormSubtitle = styled.p`
  margin: 0 0 34px;
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 15px;
`;

const Form = styled.form`
  display: grid;
  gap: 18px;
`;

const Field = styled.label`
  display: grid;
  gap: 7px;
  font-size: 13px;
  font-weight: 700;
  color: ${({ theme }) => theme.color.text};
`;

const InputWrap = styled.div`
  position: relative;
  display: flex;
  align-items: center;

  > svg:first-child {
    position: absolute;
    left: 12px;
    color: ${({ theme }) => theme.color.textMuted};
    pointer-events: none;
  }
`;

const Input = styled.input`
  width: 100%;
  min-height: 64px;
  padding: 0 42px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.lg};
  background: ${({ theme }) => theme.color.surface};
  color: ${({ theme }) => theme.color.text};
  font-size: 15px;

  &:focus {
    outline: none;
    border-color: ${({ theme }) => theme.color.brand};
    box-shadow: 0 0 0 3px color-mix(in srgb, ${({ theme }) => theme.color.brand} 16%, transparent);
  }
`;

const EyeButton = styled.button`
  position: absolute;
  right: 10px;
  display: grid;
  place-items: center;
  width: 44px;
  height: 44px;
  color: ${({ theme }) => theme.color.textMuted};
  border-radius: ${({ theme }) => theme.radius.sm};

  &:hover { color: ${({ theme }) => theme.color.text}; }
`;

const Row = styled.div`
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  font-size: 13px;
`;

const Check = styled.label`
  display: inline-flex;
  align-items: center;
  gap: 8px;
  color: ${({ theme }) => theme.color.textMuted};
  font-weight: 500;
  cursor: pointer;

  input { accent-color: ${({ theme }) => theme.color.brand}; }
`;

const LinkButton = styled.button`
  color: ${({ theme }) => theme.color.brand};
  font-weight: 700;
  font-size: 13px;

  &:hover { text-decoration: underline; }
`;

const PrimaryButton = styled.button`
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  min-height: 64px;
  border-radius: ${({ theme }) => theme.radius.lg};
  background: linear-gradient(100deg,#2c50f4 0%,#7040ea 43%,#c74bbb 68%,#ff8b22 100%);
  background-size: 100% 100%;
  color: white;
  font-size: 15px;
  font-weight: 800;
  box-shadow: 0 8px 24px rgba(124, 58, 237, 0.28);
  transition: transform 0.18s ease, box-shadow 0.18s ease, background-position 0.3s ease;

  &:hover:not(:disabled) {
    background-position: 100% 50%;
    transform: translateY(-1px);
    box-shadow: 0 10px 28px rgba(219, 39, 119, 0.3);
  }

  &:disabled { opacity: 0.7; cursor: not-allowed; }
`;

const Divider = styled.div`
  display: grid;
  grid-template-columns: 1fr auto 1fr;
  align-items: center;
  gap: 12px;
  margin: 4px 0;
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 12px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.08em;

  &::before, &::after {
    content: '';
    height: 1px;
    background: ${({ theme }) => theme.color.border};
  }
`;

const SecondaryButton = styled.button`
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  width: 100%;
  min-height: 54px;
  border: 1.5px solid ${({ theme }) => theme.color.brand};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.surface};
  color: ${({ theme }) => theme.color.brand};
  font-size: 14px;
  font-weight: 700;

  &:hover { background: color-mix(in srgb, ${({ theme }) => theme.color.brand} 6%, white); }
`;

const FooterNote = styled.p`
  margin-top: 30px;
  text-align: center;
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 13px;

  button {
    color: ${({ theme }) => theme.color.brand};
    font-weight: 700;
  }
`;

const SetupLink = styled.a`
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 7px;
  width: 100%;
  margin-top: 16px;
  padding: 11px 14px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.surface};
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 13px;
  font-weight: 700;

  &:hover {
    border-color: ${({ theme }) => theme.color.accentViolet};
    color: ${({ theme }) => theme.color.accentViolet};
  }
`;

const PageFooter = styled.footer`
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: center;
  gap: 10px 18px;
  width: 100%;
  max-width: none;
  margin: 0 auto;
  padding: 13px 20px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-top-color: ${({ theme }) => theme.color.border};
  border-radius: 0 0 22px 22px;
  background: ${({ theme }) => theme.color.surfaceSubtle};
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 12.5px;

  span { display: inline-flex; align-items: center; gap: 6px; }
  .sep { width: 1px; height: 14px; background: ${({ theme }) => theme.color.border}; }

  @media (max-width: 960px) {
    border-width: 1px 0 0;
    border-radius: 0;
  }
`;

const Alert = styled.div<{ $tone?: 'error' | 'info' }>`
  display: flex;
  gap: 8px;
  align-items: flex-start;
  margin-bottom: 16px;
  padding: 12px 14px;
  border-radius: ${({ theme }) => theme.radius.md};
  border: 1px solid ${({ theme, $tone }) => ($tone === 'error' ? theme.color.danger : theme.color.accentBlue)};
  background: ${({ theme, $tone }) =>
    $tone === 'error'
      ? `color-mix(in srgb, ${theme.color.danger} 8%, white)`
      : `color-mix(in srgb, ${theme.color.accentBlue} 8%, white)`};
  color: ${({ theme, $tone }) => ($tone === 'error' ? theme.color.danger : theme.color.text)};
  font-size: 13.5px;
  line-height: 1.45;
`;

const features = [
  { icon: Target, title: 'Planeje', text: 'Organize demandas e defina prioridades.' },
  { icon: Users, title: 'Colabore', text: 'Trabalhe em equipe em tempo real.' },
  { icon: EyeIcon, title: 'Acompanhe', text: 'Tenha total visibilidade do progresso.' },
  { icon: BarChart3, title: 'Decida', text: 'Relatórios e dados para decisões assertivas.' },
  { icon: Flag, title: 'Entregue', text: 'Mais eficiência, menos retrabalho e melhores resultados.' },
] as const;

export const Auth: React.FC = () => {
  const { mode, toggleMode } = useThemeMode();
  const initialParams = new URLSearchParams(window.location.search);
  const initialMode = initialParams.get('mode');
  const hasInvite = Boolean(initialParams.get('invite') ?? localStorage.getItem('pendingInvite'));
  const [isRegister, setIsRegister] = useState(hasInvite);
  const [flow, setFlow] = useState<'login' | 'forgot' | 'reset' | 'confirm'>(
    initialMode === 'forgot-password' ? 'forgot'
      : initialMode === 'reset-password' ? 'reset'
        : initialMode === 'confirm-email' ? 'confirm' : 'login',
  );
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [keepSignedIn, setKeepSignedIn] = useState(true);
  const [recovery, setRecovery] = useState({
    userId: initialParams.get('userId') ?? '',
    token: initialParams.get('token') ?? '',
  });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [ssoNotice, setSsoNotice] = useState<string | null>(null);
  const [setupAvailable, setSetupAvailable] = useState(false);

  useEffect(() => {
    let active = true;
    api.getSetupStatus()
      .then((status) => { if (active) setSetupAvailable(status.setupAvailable && !status.initialized); })
      .catch(() => { /* O login continua funcional quando o status não está acessível. */ });
    return () => { active = false; };
  }, []);

  useEffect(() => {
    if (flow !== 'confirm' || !recovery.userId || !recovery.token) return;
    setLoading(true);
    api.confirmEmail(recovery.userId, recovery.token).then(() => {
      setNotice('E-mail confirmado. Você já pode entrar.');
      setFlow('login');
      window.history.replaceState({}, '', window.location.pathname);
    }).catch((err) => setError(err instanceof Error ? err.message : 'Código de confirmação inválido.'))
      .finally(() => setLoading(false));
  }, [flow, recovery]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);
    setNotice(null);
    setSsoNotice(null);

    try {
      if (flow === 'forgot') {
        const result = await api.forgotPassword(email);
        if (result?.developmentToken && result?.developmentUserId) {
          setRecovery({ userId: result.developmentUserId, token: result.developmentToken });
          setFlow('reset');
          setNotice('Ambiente local: código recebido. Defina a nova senha.');
        } else {
          setNotice('Se o e-mail estiver cadastrado, você receberá as instruções de recuperação.');
        }
      } else if (flow === 'reset') {
        await api.resetPassword(recovery.userId, recovery.token, password);
        setFlow('login');
        setPassword('');
        setNotice('Senha redefinida. Entre novamente.');
        window.history.replaceState({}, '', window.location.pathname);
      } else if (isRegister) {
        const result = await api.register(email, password);
        if (result?.developmentToken && result?.userId) {
          await api.confirmEmail(result.userId, result.developmentToken);
          await api.login(email, password);
        } else {
          setIsRegister(false);
          setPassword('');
          setNotice('Cadastro criado. Confirme o e-mail enviado antes de entrar.');
        }
      } else {
        localStorage.setItem('prisma_keep_signed_in', keepSignedIn ? '1' : '0');
        await api.login(email, password);
      }
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Ocorreu um erro no processamento.');
    } finally {
      setLoading(false);
    }
  };

  const heading = flow === 'forgot'
    ? 'Recuperar senha'
    : flow === 'reset'
      ? 'Nova senha'
      : isRegister
        ? 'Criar sua conta'
        : 'Acesse sua conta';

  const subtitle = flow === 'forgot'
    ? 'Informe o e-mail para receber as instruções.'
    : flow === 'reset'
      ? 'Defina uma nova senha para continuar.'
      : isRegister
        ? 'Cadastre-se para começar no Prisma WorkSpace.'
        : 'Entre para continuar no Prisma WorkSpace.';

  return (
    <Page>
      <Shell>
        <BrandPanel aria-label="Prisma WorkSpace">
          <BrandTop>
            <BrandMark size={22} />
            Prisma WorkSpace
          </BrandTop>

          <Hero>
            <GemWrap><img src="/prisma-login-prism.svg" alt="Prisma facetado" width="194" height="187" /></GemWrap>
            <ProductName>
              PRISM<em>A</em>
              <span>WorkSpace</span>
            </ProductName>
            <SpectrumRule aria-hidden="true" />
            <Tagline>Uma visão completa do seu trabalho</Tagline>
          </Hero>

          <SpectrumWaves aria-hidden="true" viewBox="0 0 1000 180" preserveAspectRatio="none">
            <defs>
              <linearGradient id="login-spectrum" x1="0" y1="0" x2="1" y2="0">
                <stop offset="0" stopColor="#2563eb" />
                <stop offset="0.36" stopColor="#7c3aed" />
                <stop offset="0.68" stopColor="#db2777" />
                <stop offset="1" stopColor="#f97316" />
              </linearGradient>
            </defs>
            <path d="M-40 150 C150 52 260 174 430 110 S730 26 1040 124" fill="none" stroke="url(#login-spectrum)" strokeWidth="1.2" />
            <path d="M-40 164 C150 66 270 188 445 122 S744 42 1040 138" fill="none" stroke="url(#login-spectrum)" strokeWidth="1" />
            <path d="M-40 178 C165 80 282 199 462 136 S765 58 1040 152" fill="none" stroke="url(#login-spectrum)" strokeWidth="0.8" />
          </SpectrumWaves>

          <Features>
            {features.map(({ icon: Icon, title, text }) => (
              <Feature key={title}>
                <Icon size={18} strokeWidth={2.2} />
                <strong>{title}</strong>
                <span>{text}</span>
              </Feature>
            ))}
          </Features>
        </BrandPanel>

        <FormPanel>
          <ThemeChip type="button" onClick={toggleMode} aria-label="Alternar tema">
            {mode === 'light' ? <Sun size={15} /> : <Moon size={15} />}
            {mode === 'light' ? 'Modo claro' : 'Modo escuro'}
          </ThemeChip>

          <FormCard>
            <MobileBrand>
              <BrandMark size={40} />
              <ProductName style={{ fontSize: 32, textAlign: 'center' }}>
                PRISMA
                <span>Workspace</span>
              </ProductName>
            </MobileBrand>

            <FormTitle>{heading}</FormTitle>
            <FormSubtitle>{subtitle}</FormSubtitle>

            {hasInvite && (
              <Alert $tone="info">
                <Info size={16} />
                <span>
                  <strong>Você recebeu um convite para entrar em uma organização.</strong><br />
                  Use o mesmo e-mail do convite — ele será aceito automaticamente.
                </span>
              </Alert>
            )}
            {error && <Alert $tone="error"><Info size={16} /><span>{error}</span></Alert>}
            {notice && <Alert $tone="info"><Info size={16} /><span>{notice}</span></Alert>}
            {ssoNotice && <Alert $tone="info"><Shield size={16} /><span>{ssoNotice}</span></Alert>}

            <Form onSubmit={handleSubmit}>
              <Field>
                {flow === 'forgot' ? 'E-mail para recuperação' : 'E-mail corporativo'}
                <InputWrap>
                  <Mail size={16} />
                  <Input
                    id="email"
                    type="email"
                    placeholder="seu.nome@empresa.com"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    autoComplete="username"
                    required
                  />
                </InputWrap>
              </Field>

              {flow !== 'forgot' && flow !== 'confirm' && (
                <Field>
                  Senha
                  <InputWrap>
                    <Lock size={16} />
                    <Input
                      id="password"
                      type={showPassword ? 'text' : 'password'}
                      placeholder="Digite sua senha"
                      value={password}
                      onChange={(e) => setPassword(e.target.value)}
                      autoComplete={isRegister || flow === 'reset' ? 'new-password' : 'current-password'}
                      minLength={isRegister || flow === 'reset' ? 10 : undefined}
                      required
                    />
                    <EyeButton
                      type="button"
                      aria-label={showPassword ? 'Ocultar senha' : 'Mostrar senha'}
                      onClick={() => setShowPassword((v) => !v)}
                    >
                      {showPassword ? <EyeOff size={16} /> : <Eye size={16} />}
                    </EyeButton>
                  </InputWrap>
                </Field>
              )}

              {(isRegister || flow === 'reset') && (
                <Alert $tone="info">
                  <Info size={16} />
                  <span>A senha deve conter ao menos 10 caracteres, com maiúscula, minúscula, número e caractere especial.</span>
                </Alert>
              )}

              {flow === 'login' && !isRegister && (
                <Row>
                  <Check>
                    <input
                      type="checkbox"
                      checked={keepSignedIn}
                      onChange={(e) => setKeepSignedIn(e.target.checked)}
                    />
                    Manter conectado
                  </Check>
                  <LinkButton type="button" onClick={() => { setFlow('forgot'); setError(null); }}>
                    Esqueceu sua senha?
                  </LinkButton>
                </Row>
              )}

              <PrimaryButton type="submit" disabled={loading}>
                {loading ? 'Processando...' : flow === 'forgot' ? 'Enviar instruções' : flow === 'reset' ? 'Redefinir senha' : isRegister ? (
                  <><UserPlus size={18} /><span>Criar conta</span></>
                ) : (
                  <><LogIn size={18} /><span>Entrar →</span></>
                )}
              </PrimaryButton>
            </Form>

            {flow === 'login' && !isRegister && (
              <>
                <Divider>ou</Divider>
                <SecondaryButton
                  type="button"
                  onClick={() => setSsoNotice('SSO corporativo estará disponível nas edições Cloud e Enterprise.')}
                >
                  <Shield size={16} />
                  Entrar com SSO (Conta corporativa)
                </SecondaryButton>
              </>
            )}

            <FooterNote>
              {flow === 'forgot' || flow === 'reset' || flow === 'confirm' ? (
                <button type="button" onClick={() => { setFlow('login'); setError(null); }}>Voltar para o login</button>
              ) : isRegister ? (
                <>Já possui uma conta? <button type="button" onClick={() => setIsRegister(false)}>Entrar aqui</button></>
              ) : (
                <>Ainda não tem conta? <button type="button" onClick={() => setIsRegister(true)}>Fale com o administrador</button> ou <button type="button" onClick={() => setIsRegister(true)}>registre-se</button>.</>
              )}
            </FooterNote>

            {flow === 'login' && !isRegister && setupAvailable && (
              <SetupLink href="/setup"><KeyRound size={15} /> Configurar esta instalação</SetupLink>
            )}
          </FormCard>
        </FormPanel>
      </Shell>

      <PageFooter>
        <span><Lock size={12} />Ambiente seguro e monitorado</span>
        <span className="sep" aria-hidden />
        <span>Seus dados estão protegidos conforme a LGPD</span>
      </PageFooter>
    </Page>
  );
};
