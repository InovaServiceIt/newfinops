import React, { useState } from 'react';
import { Wand2, HelpCircle, X } from 'lucide-react';
import { patternAnalysisService, PatternAnalysisStatus, ProgressJob } from '../../../services/patternAnalysisService';
import { PatternBlockedModal, PatternErrorModal, PatternSuccessModal } from './PatternAnalysisModals';
import { PatternAnalysisProcessing } from './PatternAnalysisProcessing';
import './PatternAnalysisProcessing.css';

export function PatternAnalysisTrigger() {
  const [patternStatus, setPatternStatus] = useState<PatternAnalysisStatus | null>(null);
  const [isProcessing, setIsProcessing] = useState(false);
  const [jobId, setJobId] = useState<string>('');
  const [successData, setSuccessData] = useState<ProgressJob | null>(null);
  const [discoveryResult, setDiscoveryResult] = useState<{ inseridas: number; ignoradas: number; totalView: number } | null>(null);
  const [errorData, setErrorData] = useState<{ msg: string } | null>(null);
  const [showHowItWorks, setShowHowItWorks] = useState(false);
  const [howItWorksPage, setHowItWorksPage] = useState(1);

  const [initialStatus, setInitialStatus] = useState<PatternAnalysisStatus | null>(null);

  React.useEffect(() => {
    const fetchStatus = async () => {
      try {
        const status = await patternAnalysisService.getStatus();
        setInitialStatus(status);
      } catch (err) {
        console.error("Erro ao buscar status inicial:", err);
      }
    };
    fetchStatus();
  }, []);

  const handleStartAnalysis = async (force = false) => {
    try {
      setPatternStatus(null);
      const res = await patternAnalysisService.executar(force);
      
      if (res && 'bloqueado' in res) {
         setPatternStatus(res as unknown as PatternAnalysisStatus);
         return;
      }

      setJobId(res.jobId);
      setIsProcessing(true);
    } catch (err: any) {
      setErrorData({ msg: err.message });
    }
  };

  const renderBacklogStatus = () => {
    if (!initialStatus) return null;
    
    return (
      <div style={{
        marginTop: '0.5rem',
        fontSize: '11px',
        color: '#888',
        display: 'flex',
        flexDirection: 'column',
        gap: '2px',
        padding: '4px 8px',
        background: '#F9F9F9',
        borderRadius: '4px',
        borderLeft: '2px solid #0078D4'
      }}>
        <div style={{ display: 'flex', justifyContent: 'space-between' }}>
          <span>Vetorizados:</span>
          <span style={{ fontWeight: 700, color: '#333' }}>{initialStatus.qtAvaliados}</span>
        </div>
        <div style={{ display: 'flex', justifyContent: 'space-between' }}>
          <span>Pendentes:</span>
          <span style={{ fontWeight: 700, color: '#0078D4' }}>{initialStatus.qtFaltaAvaliar}</span>
        </div>
      </div>
    );
  };

  return (
    <>
      <div style={{ display: 'flex', flexDirection: 'column', width: '200px' }}>
        <button 
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '0.5rem',
            height: '36px',
            background: 'transparent',
            border: '1px solid #DEDEDE',
            color: '#555',
            borderRadius: '4px',
            padding: '0 1rem',
            fontSize: '13px',
            fontWeight: 600,
            cursor: 'pointer',
            transition: 'all 0.2s',
            outline: 'none',
            width: '100%'
          }}
          onClick={() => handleStartAnalysis(false)} 
          disabled={isProcessing}
          onMouseOver={e => e.currentTarget.style.background = '#F6F6F6'}
          onMouseOut={e => e.currentTarget.style.background = 'transparent'}
        >
          <Wand2 size={16} />
          Analisar novos padrões
        </button>
        <button
          onClick={() => setShowHowItWorks(true)}
          title="Como funciona?"
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '0.4rem',
            marginTop: '6px',
            background: 'transparent',
            border: 'none',
            color: '#888',
            fontSize: '11px',
            cursor: 'pointer',
            padding: '2px 0',
          }}
        >
          <HelpCircle size={13} />
          Como funciona?
        </button>
        {renderBacklogStatus()}
      </div>

      {/* OVERLAY DE PROCESSAMENTO */}
      {isProcessing && jobId && (
        <PatternAnalysisProcessing
          jobId={jobId}
          onComplete={(res) => { 
            if (res.discoveryResult) {
              setDiscoveryResult(res.discoveryResult);
            }
            setIsProcessing(false); 
            setJobId(''); 
            setSuccessData(res); 
          }}
          onError={(errData) => { setIsProcessing(false); setJobId(''); setErrorData({ msg: errData.erro || 'Erro desconhecido' }); }}
          onGoToApproval={() => { window.location.href = '/approval'; }}
        />
      )}

      {/* MODAL 1: BLOQUEADO */}
      {patternStatus && patternStatus.bloqueado && (
        <PatternBlockedModal
          status={patternStatus}
          isAdmin={true} // Emulando admin logado
          onClose={() => setPatternStatus(null)}
          onGoToApproval={() => { window.location.href = '/approval'; }}
          onForceRun={() => handleStartAnalysis(true)}
        />
      )}

      {/* MODAL 2: SUCESSO */}
      {successData && (
        <PatternSuccessModal
          result={successData}
          discoveryResult={discoveryResult || undefined}
          onClose={() => { setSuccessData(null); setDiscoveryResult(null); }}
          onGoToApproval={() => { window.location.href = '/approval'; }}
        />
      )}

      {/* MODAL 3: ERRO */}
      {errorData && (
        <PatternErrorModal
          errorMsg={errorData.msg}
          onClose={() => setErrorData(null)}
          onRetry={() => { setErrorData(null); handleStartAnalysis(true); }}
        />
      )}
      {/* MODAL COMO FUNCIONA */}
      {showHowItWorks && (
        <div style={{
          position: 'fixed', top: 0, left: 0, width: '100vw', height: '100vh',
          background: 'rgba(15,23,42,0.6)', backdropFilter: 'blur(4px)',
          zIndex: 9999, display: 'flex', alignItems: 'center', justifyContent: 'center'
        }}>
          <div style={{
            background: '#fff', borderRadius: '12px', padding: '2.5rem',
            maxWidth: '600px', width: '90%', position: 'relative',
            boxShadow: '0 20px 60px rgba(0,0,0,0.2)', maxHeight: '85vh', overflowY: 'auto'
          }}>
            <button onClick={() => { setShowHowItWorks(false); setHowItWorksPage(1); }} style={{
              position: 'absolute', top: '1rem', right: '1rem',
              background: 'transparent', border: 'none', cursor: 'pointer', color: '#888'
            }}>
              <X size={20} />
            </button>

            <h3 style={{ margin: '0 0 1.5rem 0', fontSize: '1.2rem', color: '#111' }}>Como funciona a análise de padrões?</h3>

            {/* PAGE 1 */}
            {howItWorksPage === 1 && (
              <div style={{ fontSize: '0.9rem', lineHeight: '1.75', color: '#333' }}>
                <p>Imagina que você tem um analista de plantão que lê <strong>todos</strong> os alertas de TI que chegam — "servidor fora do ar", "disco cheio", "serviço lento" — 24 horas por dia.</p>
                <p style={{ marginTop: '1rem' }}>O problema é que esse analista é meio literalista. Se chegar "disco cheio no servidor A" e depois "armazenamento esgotado no server A", ele trata como <strong>dois problemas diferentes</strong> porque as palavras são diferentes. Aí você tem ticket duplicado, analista humano olhando a mesma coisa duas vezes, retrabalho.</p>
                <p style={{ marginTop: '1rem' }}>O que o Qwen3-Embedding faz é dar a esse analista uma <strong>inteligência semântica</strong>. Toda vez que um alerta chega, ele não lê as palavras — ele entende o <em>significado</em> e transforma aquele alerta em 1024 números que representam a "essência" daquele problema.</p>
                <p style={{ marginTop: '1rem' }}>"Disco cheio no servidor A" vira um conjunto de números. "Armazenamento esgotado no server A" vira um conjunto de números <strong>muito parecido</strong>.</p>
                <p style={{ marginTop: '1rem' }}>Aí o Predict compara os dois conjuntos e percebe: "olha, esses dois alertas estão falando da mesma coisa" — e agrupa, deduplica, ou sinaliza como padrão recorrente.</p>
              </div>
            )}

            {/* PAGE 2 */}
            {howItWorksPage === 2 && (
              <div style={{ fontSize: '0.88rem', lineHeight: '1.7', color: '#333' }}>
                <div style={{ fontWeight: 700, fontSize: '1rem', marginBottom: '1rem', color: '#111', textAlign: 'center' }}>
                  Dois alertas — mesmo problema, textos diferentes
                </div>

                {/* Vetores */}
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem', marginBottom: '1rem' }}>
                  <div style={{ background: '#f0f6ff', borderRadius: '8px', padding: '0.75rem', borderLeft: '3px solid #4A90D9' }}>
                    <div style={{ fontWeight: 700, fontSize: '0.8rem', marginBottom: '0.2rem', color: '#4A90D9' }}>Alerta A</div>
                    <div style={{ fontSize: '0.8rem', fontStyle: 'italic', marginBottom: '0.3rem' }}>"Disco cheio no servidor A"</div>
                    <div style={{ fontFamily: 'monospace', fontWeight: 700, color: '#4A90D9' }}>A = [3, 2, 0]</div>
                  </div>
                  <div style={{ background: '#fff5ed', borderRadius: '8px', padding: '0.75rem', borderLeft: '3px solid #E67300' }}>
                    <div style={{ fontWeight: 700, fontSize: '0.8rem', marginBottom: '0.2rem', color: '#E67300' }}>Alerta B</div>
                    <div style={{ fontSize: '0.8rem', fontStyle: 'italic', marginBottom: '0.3rem' }}>"Armazenamento esgotado no server A"</div>
                    <div style={{ fontFamily: 'monospace', fontWeight: 700, color: '#E67300' }}>B = [4, 1, 0]</div>
                  </div>
                </div>

                {/* Passos */}
                <div style={{ fontWeight: 700, fontSize: '0.85rem', marginBottom: '0.5rem', color: '#111' }}>Cálculo passo a passo</div>
                {[
                  { step: 'Passo 1 — produto escalar (A · B)', line: '(3×4) + (2×1) + (0×0) = 12 + 2 + 0 = 14' },
                  { step: 'Passo 2 — magnitudes', line: '‖A‖ = √(9 + 4 + 0) = √13 ≈ 3.606\n‖B‖ = √(16 + 1 + 0) = √17 ≈ 4.123' },
                  { step: 'Passo 3 — similaridade cosseno', line: '14 / (3.606 × 4.123) = 14 / 14.868 ≈ 0.942' },
                  { step: 'Passo 4 — distância cosseno', line: '1 − 0.942 = 0.058' },
                ].map((block, i) => (
                  <div key={i} style={{ marginBottom: '0.5rem', background: '#fafafa', borderRadius: '7px', padding: '0.55rem 0.8rem', border: '1px solid #eee' }}>
                    <div style={{ fontWeight: 600, fontSize: '0.8rem', marginBottom: '0.2rem', color: '#444' }}>{block.step}</div>
                    {block.line.split('\n').map((l, j) => (
                      <div key={j} style={{ fontFamily: 'monospace', fontSize: '0.83rem', color: '#222' }}>{l}</div>
                    ))}
                  </div>
                ))}

                {/* Resultado */}
                <div style={{ background: '#e8f5e9', borderRadius: '10px', padding: '1rem', border: '1px solid #a5d6a7', marginTop: '0.75rem' }}>
                  <div style={{ fontWeight: 700, fontSize: '0.82rem', color: '#2e7d32', marginBottom: '0.75rem', textAlign: 'center' }}>Resultado</div>
                  <div style={{ display: 'grid', gridTemplateColumns: '1fr auto 1fr auto 1fr', alignItems: 'center', gap: '0.5rem', textAlign: 'center', marginBottom: '0.85rem' }}>
                    <div>
                      <div style={{ fontSize: '0.72rem', color: '#555', marginBottom: '0.2rem' }}>Similaridade</div>
                      <div style={{ fontFamily: 'monospace', fontSize: '1.5rem', fontWeight: 800, color: '#2e7d32' }}>0.942</div>
                    </div>
                    <div style={{ fontSize: '1.2rem', color: '#999' }}>→</div>
                    <div>
                      <div style={{ fontSize: '0.72rem', color: '#555', marginBottom: '0.2rem' }}>Distância cosseno</div>
                      <div style={{ fontFamily: 'monospace', fontSize: '1.5rem', fontWeight: 800, color: '#1565c0' }}>0.058</div>
                    </div>
                    <div style={{ fontSize: '1rem', color: '#999', fontWeight: 700 }}>vs</div>
                    <div>
                      <div style={{ fontSize: '0.72rem', color: '#555', marginBottom: '0.2rem' }}>Threshold</div>
                      <div style={{ fontFamily: 'monospace', fontSize: '1.5rem', fontWeight: 800, color: '#c62828' }}>0.100</div>
                    </div>
                  </div>
                  <div style={{ background: '#fff', borderRadius: '7px', padding: '0.6rem', textAlign: 'center', border: '1px solid #c8e6c9' }}>
                    <div style={{ fontFamily: 'monospace', fontWeight: 700, color: '#2e7d32', fontSize: '0.9rem' }}>0.058 &lt; 0.10 → mesmo problema</div>
                    <div style={{ fontSize: '0.8rem', color: '#388e3c', marginTop: '0.2rem' }}>Predict agrupa — nenhum ticket novo gerado</div>
                  </div>
                </div>

                <p style={{ marginTop: '0.85rem', fontSize: '0.8rem', color: '#666', fontStyle: 'italic', lineHeight: '1.5' }}>
                  Textos completamente diferentes, mesma essência. A distância cosseno de 0.058 está bem abaixo do threshold 0.10 — o Predict reconhece que são o mesmo problema e suprime a duplicata.
                </p>
              </div>
            )}

            {/* NAVIGATION */}
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginTop: '1.5rem', borderTop: '1px solid #eee', paddingTop: '1rem' }}>
              <button
                onClick={() => setHowItWorksPage(p => Math.max(1, p - 1))}
                disabled={howItWorksPage === 1}
                style={{ background: 'transparent', border: '1px solid #ddd', borderRadius: '6px', padding: '0.4rem 1rem', cursor: howItWorksPage === 1 ? 'not-allowed' : 'pointer', opacity: howItWorksPage === 1 ? 0.35 : 1, fontSize: '0.85rem' }}>
                ← Anterior
              </button>
              <span style={{ fontSize: '0.8rem', color: '#aaa' }}>{howItWorksPage} / 2</span>
              <button
                onClick={() => setHowItWorksPage(p => Math.min(2, p + 1))}
                disabled={howItWorksPage === 2}
                style={{ background: howItWorksPage === 2 ? 'transparent' : '#111', color: howItWorksPage === 2 ? '#aaa' : '#fff', border: '1px solid #ddd', borderRadius: '6px', padding: '0.4rem 1rem', cursor: howItWorksPage === 2 ? 'not-allowed' : 'pointer', opacity: howItWorksPage === 2 ? 0.35 : 1, fontSize: '0.85rem', transition: 'all 0.2s' }}>
                Próximo →
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
