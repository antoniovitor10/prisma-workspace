import { useEffect, useState, type FormEvent } from 'react';
import { ArrowRight, Building2, Check, Eye, EyeOff, KeyRound, LockKeyhole, Mail, Moon, ShieldCheck, Sun, UserRound } from 'lucide-react';
import styled from 'styled-components';
import { BrandGem, BrandMark } from '../components/BrandMark';
import { api, SetupApiError, type SetupInput, type SetupStatus } from '../services/api';
import { useThemeMode } from '../styles/ThemeMode';

type ViewState = 'loading' | 'ready' | 'unavailable' | 'completed' | 'error';

const Page = styled.main`
  min-height: 100vh;
  display: grid;
  grid-template-columns: minmax(340px, 0.82fr) minmax(560px, 1.18fr);
  background: ${({ theme }) => theme.color.bg};
  color: ${({ theme }) => theme.color.text};

  @media (max-width: 900px) { grid-template-columns: 1fr; }
`;

const BrandPanel = styled.section`
  position: relative;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  min-height: 100vh;
  padding: clamp(28px, 4vw, 64px);
  overflow: hidden;
  border-right: 1px solid ${({ theme }) => theme.color.border};
  background:
    radial-gradient(circle at 24% 30%, rgba(124, 58, 237, 0.18), transparent 28%),
    radial-gradient(circle at 78% 76%, rgba(249, 115, 22, 0.12), transparent 30%),
    ${({ theme }) => theme.color.surface};

  &::after {
    content: '';
    position: absolute;
    width: 440px;
    height: 440px;
    left: -180px;
    bottom: -220px;
    border: 1px solid rgba(124, 58, 237, 0.18);
    border-radius: 50%;
    box-shadow: 0 0 0 64px rgba(37, 99, 235, 0.035), 0 0 0 128px rgba(219, 39, 119, 0.025);
  }

  @media (max-width: 900px) {
    min-height: auto;
    padding: 24px;
    border-right: 0;
    border-bottom: 1px solid ${({ theme }) => theme.color.border};
  }
`;

const Brand = styled.div`
  position: relative;
  z-index: 1;
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 14px;
  font-weight: 800;
  letter-spacing: -0.01em;
`;

const BrandCopy = styled.div`
  position: relative;
  z-index: 1;
  max-width: 510px;

  h1 {
    margin: 22px 0 12px;
    font-size: clamp(34px, 4vw, 58px);
    line-height: 1.02;
    letter-spacing: -0.045em;
  }

  p {
    margin: 0;
    max-width: 440px;
    color: ${({ theme }) => theme.color.textMuted};
    font-size: 17px;
    line-height: 1.65;
  }

  @media (max-width: 900px) {
    display: none;
  }
`;

const Trust = styled.div`
  position: relative;
  z-index: 1;
  display: flex;
  align-items: center;
  gap: 10px;
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 13px;

  svg { color: ${({ theme }) => theme.color.accentGreen}; }
  @media (max-width: 900px) { display: none; }
`;

const Content = styled.section`
  position: relative;
  display: grid;
  place-items: center;
  min-height: 100vh;
  padding: 80px clamp(22px, 6vw, 96px) 56px;

  @media (max-width: 900px) {
    min-height: auto;
    padding: 72px 20px 40px;
  }
`;

const ThemeButton = styled.button`
  position: absolute;
  top: 24px;
  right: 28px;
  display: inline-flex;
  align-items: center;
  gap: 8px;
  min-height: 38px;
  padding: 0 12px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.pill};
  background: ${({ theme }) => theme.color.surface};
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 13px;
  font-weight: 700;
`;

const Card = styled.div`
  width: min(100%, 760px);
`;

const Eyebrow = styled.span`
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 7px 11px;
  border: 1px solid color-mix(in srgb, ${({ theme }) => theme.color.accentViolet} 22%, transparent);
  border-radius: ${({ theme }) => theme.radius.pill};
  background: color-mix(in srgb, ${({ theme }) => theme.color.accentViolet} 7%, transparent);
  color: ${({ theme }) => theme.color.accentViolet};
  font-size: 12px;
  font-weight: 800;
  letter-spacing: 0.04em;
  text-transform: uppercase;
`;

const Title = styled.h2`
  margin: 18px 0 8px;
  font-size: clamp(28px, 3vw, 40px);
  line-height: 1.1;
  letter-spacing: -0.035em;
`;

const Subtitle = styled.p`
  margin: 0 0 30px;
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 15px;
  line-height: 1.6;
`;

const Form = styled.form`
  display: grid;
  gap: 24px;
`;

const Section = styled.fieldset`
  display: grid;
  gap: 16px;
  margin: 0;
  padding: 22px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.xl};
  background: ${({ theme }) => theme.color.surface};
  box-shadow: ${({ theme }) => theme.shadow.sm};

  legend {
    padding: 0 8px;
    font-size: 13px;
    font-weight: 800;
    color: ${({ theme }) => theme.color.textMuted};
  }
`;

const Grid = styled.div`
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 16px;

  @media (max-width: 620px) { grid-template-columns: 1fr; }
`;

const Field = styled.label`
  display: grid;
  gap: 7px;
  font-size: 13px;
  font-weight: 750;
`;

const Hint = styled.span`
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 11.5px;
  font-weight: 500;
  line-height: 1.4;
`;

const InputWrap = styled.div`
  position: relative;
  display: flex;
  align-items: center;

  > svg:first-child {
    position: absolute;
    left: 13px;
    color: ${({ theme }) => theme.color.textMuted};
    pointer-events: none;
  }
`;

const Input = styled.input`
  width: 100%;
  min-height: 50px;
  padding: 0 42px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.surfaceSubtle};
  color: ${({ theme }) => theme.color.text};
  font-size: 14px;

  &:focus {
    outline: none;
    border-color: ${({ theme }) => theme.color.brand};
    box-shadow: 0 0 0 3px color-mix(in srgb, ${({ theme }) => theme.color.brand} 14%, transparent);
  }

  &::placeholder { color: ${({ theme }) => theme.color.textSubtle}; }
`;

const Reveal = styled.button`
  position: absolute;
  right: 10px;
  display: grid;
  place-items: center;
  width: 32px;
  height: 32px;
  color: ${({ theme }) => theme.color.textMuted};
  border-radius: ${({ theme }) => theme.radius.sm};
`;

const Alert = styled.div<{ $tone?: 'danger' | 'info' | 'success' }>`
  display: flex;
  gap: 10px;
  align-items: flex-start;
  padding: 14px 16px;
  border: 1px solid ${({ theme, $tone }) => $tone === 'danger' ? theme.color.danger : $tone === 'success' ? theme.color.accentGreen : theme.color.accentBlue};
  border-radius: ${({ theme }) => theme.radius.md};
  background: color-mix(in srgb, ${({ theme, $tone }) => $tone === 'danger' ? theme.color.danger : $tone === 'success' ? theme.color.accentGreen : theme.color.accentBlue} 7%, ${({ theme }) => theme.color.surface});
  color: ${({ theme, $tone }) => $tone === 'danger' ? theme.color.danger : theme.color.text};
  font-size: 13px;
  line-height: 1.5;
`;

const Actions = styled.div`
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 18px;

  @media (max-width: 620px) { align-items: stretch; flex-direction: column-reverse; }
`;

const BackLink = styled.button`
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 13px;
  font-weight: 700;

  &:hover { color: ${({ theme }) => theme.color.brand}; }
`;

const Submit = styled.button`
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 9px;
  min-height: 52px;
  padding: 0 22px;
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.gradient};
  color: white;
  font-size: 14px;
  font-weight: 800;
  box-shadow: 0 10px 26px rgba(124, 58, 237, 0.24);

  &:disabled { cursor: not-allowed; opacity: 0.64; }
`;

const StateCard = styled.div`
  padding: clamp(28px, 5vw, 48px);
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.xl};
  background: ${({ theme }) => theme.color.surface};
  box-shadow: ${({ theme }) => theme.shadow.md};
  text-align: center;

  svg { color: ${({ theme }) => theme.color.accentViolet}; }
  h2 { margin: 20px 0 8px; font-size: 28px; }
  p { margin: 0 auto 24px; max-width: 480px; color: ${({ theme }) => theme.color.textMuted}; line-height: 1.6; }
`;

const initialInput: SetupInput = {
  administratorName: '',
  administratorEmail: '',
  administratorPassword: '',
  organizationName: '',
  organizationSlug: '',
};

function stateFromStatus(status: SetupStatus): ViewState {
  if (status.initialized) return 'completed';
  return status.setupAvailable ? 'ready' : 'unavailable';
}

export function Setup({ onExit }: { onExit: (setupCompleted: boolean) => void }) {
  const { mode, toggleMode } = useThemeMode();
  const [view, setView] = useState<ViewState>('loading');
  const [input, setInput] = useState<SetupInput>(initialInput);
  const [setupToken, setSetupToken] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    api.getSetupStatus()
      .then((status) => { if (active) setView(stateFromStatus(status)); })
      .catch(() => { if (active) setView('error'); });
    return () => { active = false; };
  }, []);

  const update = (field: keyof SetupInput, value: string) => {
    setInput((current) => ({ ...current, [field]: value }));
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setSubmitting(true);
    setMessage(null);

    try {
      await api.completeSetup(setupToken, input);
      setSetupToken('');
      setInput(initialInput);
      setView('completed');
      onExit(true);
    } catch (error) {
      setSetupToken('');
      if (error instanceof SetupApiError) {
        if (error.status === 403 || error.code === 'setup_unavailable') {
          setView('unavailable');
          return;
        }
        if (error.code === 'setup_already_completed') {
          setView('completed');
          return;
        }
        if (error.status === 409 || error.code === 'setup_conflict') {
          setMessage('Já existem dados incompatíveis com uma instalação inicial. Revise o banco e tente novamente.');
          return;
        }
        if (error.status === 400) {
          setMessage(error.message);
          return;
        }
      }
      setMessage('Não foi possível concluir a configuração. Tente novamente.');
    } finally {
      setSubmitting(false);
    }
  };

  const goToLogin = () => onExit(false);

  return (
    <Page>
      <BrandPanel aria-label="Prisma WorkSpace">
        <Brand><BrandMark size={28} /> Prisma WorkSpace</Brand>
        <BrandCopy>
          <BrandGem size={118} />
          <h1>Seu ambiente,<br />pronto para começar.</h1>
          <p>Configure a organização e a primeira conta administradora. Esta etapa acontece uma única vez.</p>
        </BrandCopy>
        <Trust><ShieldCheck size={18} /> O código de instalação não será armazenado.</Trust>
      </BrandPanel>

      <Content>
        <ThemeButton type="button" onClick={toggleMode} aria-label="Alternar tema">
          {mode === 'light' ? <Sun size={15} /> : <Moon size={15} />}
          {mode === 'light' ? 'Modo claro' : 'Modo escuro'}
        </ThemeButton>

        <Card>
          {view === 'loading' && (
            <StateCard aria-live="polite">
              <BrandMark size={42} />
              <h2>Verificando a instalação</h2>
              <p>Estamos confirmando se este ambiente está pronto para a configuração inicial.</p>
            </StateCard>
          )}

          {view === 'error' && (
            <StateCard role="alert">
              <LockKeyhole size={42} />
              <h2>Não foi possível verificar o ambiente</h2>
              <p>Confirme se a API está acessível e recarregue esta página.</p>
              <Submit type="button" onClick={() => window.location.reload()}>Tentar novamente</Submit>
            </StateCard>
          )}

          {view === 'unavailable' && (
            <StateCard>
              <LockKeyhole size={42} />
              <h2>Configuração indisponível</h2>
              <p>O administrador do servidor precisa habilitar a configuração inicial antes de continuar.</p>
              <Submit type="button" onClick={goToLogin}>Ir para o login <ArrowRight size={17} /></Submit>
            </StateCard>
          )}

          {view === 'completed' && (
            <StateCard>
              <Check size={44} />
              <h2>Ambiente já configurado</h2>
              <p>A configuração inicial foi concluída. Entre com a conta administradora criada.</p>
              <Submit type="button" onClick={goToLogin}>Acessar o Prisma <ArrowRight size={17} /></Submit>
            </StateCard>
          )}

          {view === 'ready' && (
            <>
              <Eyebrow><ShieldCheck size={14} /> Configuração segura</Eyebrow>
              <Title>Prepare seu Prisma WorkSpace</Title>
              <Subtitle>Informe os dados essenciais. Você poderá completar o perfil da organização depois.</Subtitle>

              {message && <Alert $tone="danger" role="alert"><LockKeyhole size={17} /><span>{message}</span></Alert>}

              <Form onSubmit={submit}>
                <Section>
                  <legend>1. Segurança da instalação</legend>
                  <Field>
                    Código de instalação
                    <InputWrap>
                      <KeyRound size={17} />
                      <Input
                        type="password"
                        value={setupToken}
                        onChange={(event) => setSetupToken(event.target.value)}
                        placeholder="Cole o código gerado no servidor"
                        autoComplete="off"
                        spellCheck={false}
                        required
                      />
                    </InputWrap>
                    <Hint>Enviado com segurança e descartado após esta tentativa.</Hint>
                  </Field>
                </Section>

                <Section>
                  <legend>2. Conta administradora</legend>
                  <Grid>
                    <Field>
                      Nome completo
                      <InputWrap><UserRound size={17} /><Input value={input.administratorName} onChange={(event) => update('administratorName', event.target.value)} autoComplete="name" placeholder="Seu nome" required /></InputWrap>
                    </Field>
                    <Field>
                      E-mail
                      <InputWrap><Mail size={17} /><Input type="email" value={input.administratorEmail} onChange={(event) => update('administratorEmail', event.target.value)} autoComplete="username" placeholder="voce@empresa.com" required /></InputWrap>
                    </Field>
                  </Grid>
                  <Field>
                    Senha
                    <InputWrap>
                      <LockKeyhole size={17} />
                      <Input type={showPassword ? 'text' : 'password'} value={input.administratorPassword} onChange={(event) => update('administratorPassword', event.target.value)} autoComplete="new-password" placeholder="Crie uma senha forte" minLength={10} required />
                      <Reveal type="button" onClick={() => setShowPassword((current) => !current)} aria-label={showPassword ? 'Ocultar senha' : 'Mostrar senha'}>{showPassword ? <EyeOff size={17} /> : <Eye size={17} />}</Reveal>
                    </InputWrap>
                    <Hint>Mínimo de 10 caracteres, com maiúscula, minúscula, número e caractere especial.</Hint>
                  </Field>
                </Section>

                <Section>
                  <legend>3. Organização</legend>
                  <Grid>
                    <Field>
                      Nome da organização
                      <InputWrap><Building2 size={17} /><Input value={input.organizationName} onChange={(event) => update('organizationName', event.target.value)} autoComplete="organization" placeholder="Minha empresa" required /></InputWrap>
                    </Field>
                    <Field>
                      Identificador (slug)
                      <InputWrap><Building2 size={17} /><Input value={input.organizationSlug} onChange={(event) => update('organizationSlug', event.target.value.toLowerCase().replace(/[^a-z0-9-]/g, ''))} pattern="[a-z0-9]+(?:-[a-z0-9]+)*" minLength={3} placeholder="minha-empresa" required /></InputWrap>
                      <Hint>Apenas letras minúsculas, números e hífens.</Hint>
                    </Field>
                  </Grid>
                </Section>

                <Actions>
                  <BackLink type="button" onClick={goToLogin}>Voltar para o login</BackLink>
                  <Submit type="submit" disabled={submitting}>{submitting ? 'Configurando...' : <>Concluir configuração <ArrowRight size={17} /></>}</Submit>
                </Actions>
              </Form>
            </>
          )}
        </Card>
      </Content>
    </Page>
  );
}
