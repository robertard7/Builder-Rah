import React, { useEffect, useMemo, useState, useCallback } from 'react';
import SessionControls from './components/SessionControls.jsx';
import PlanEditor from './components/PlanEditor.jsx';
import OutputCards from './components/OutputCards.jsx';
import Streamer from './components/Streamer.jsx';
import ClarifierPrompt from './components/ClarifierPrompt.jsx';
import ArtifactExplorer from './components/ArtifactExplorer.jsx';
import Docs from './components/Docs.jsx';

const LOCAL_STORAGE_KEY = 'builder-rah-sessions';

function useToast() {
  const [toasts, setToasts] = useState([]);

  const pushToast = useCallback((message, type = 'info') => {
    const id = crypto.randomUUID();
    setToasts((prev) => [...prev, { id, message, type }]);
    setTimeout(() => {
      setToasts((prev) => prev.filter((toast) => toast.id !== id));
    }, 4200);
  }, []);

  return { toasts, pushToast };
}

function App() {
  const storedSessions = useMemo(() => {
    try {
      const value = localStorage.getItem(LOCAL_STORAGE_KEY);
      return value ? JSON.parse(value) : [];
    } catch (err) {
      console.error('Failed to read sessions', err);
      return [];
    }
  }, []);

  const [sessions, setSessions] = useState(storedSessions);
  const [sessionToken, setSessionToken] = useState(storedSessions[0] || '');
  const [planSteps, setPlanSteps] = useState([]);
  const [cards, setCards] = useState([]);
  const [artifactTree, setArtifactTree] = useState(null);
  const [view, setView] = useState('dashboard');
  const { toasts, pushToast } = useToast();
  const [loadingPlan, setLoadingPlan] = useState(false);
  const [loadingArtifacts, setLoadingArtifacts] = useState(false);
  const [outputStatus, setOutputStatus] = useState('idle');

  useEffect(() => {
    localStorage.setItem(LOCAL_STORAGE_KEY, JSON.stringify(sessions));
  }, [sessions]);

  const addSession = (token) => {
    setSessions((prev) => {
      if (prev.includes(token)) {
        return prev;
      }
      return [token, ...prev];
    });
    setSessionToken(token);
  };

  const handleNewSession = async () => {
    try {
      const response = await fetch('/api/jobs', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' }
      });
      if (!response.ok) {
        throw new Error('Failed to create session');
      }
      const data = await response.json();
      const token = data.session || data.sessionToken || data.token;
      if (!token) {
        throw new Error('Session token missing in response');
      }
      addSession(token);
      pushToast('Session created', 'success');
    } catch (err) {
      pushToast(err.message, 'error');
    }
  };

  const handleResetSession = async (token) => {
    if (!token) return;
    try {
      const response = await fetch(`/api/session/reset?session=${encodeURIComponent(token)}`, {
        method: 'POST'
      });
      if (!response.ok) {
        throw new Error('Failed to reset session');
      }
      pushToast('Session reset', 'success');
      await fetchPlan(token);
      await fetchOutputs(token);
      await fetchArtifacts(token);
    } catch (err) {
      pushToast(err.message, 'error');
    }
  };

  const fetchPlan = useCallback(async (token) => {
    if (!token) return;
    setLoadingPlan(true);
    try {
      const response = await fetch(`/api/plan?session=${encodeURIComponent(token)}`);
      if (!response.ok) {
        throw new Error('Failed to load plan');
      }
      const data = await response.json();
      const steps = Array.isArray(data.steps)
        ? data.steps
        : Array.isArray(data)
          ? data
          : [];
      const normalized = steps.map((step, index) => ({
        id: step.id || `${index}-${Date.now()}`,
        text: step.text || step.title || String(step)
      }));
      setPlanSteps(normalized);
    } catch (err) {
      pushToast(err.message, 'error');
    } finally {
      setLoadingPlan(false);
    }
  }, [pushToast]);

  const fetchOutputs = useCallback(async (token) => {
    if (!token) return;
    setOutputStatus('loading');
    try {
      const response = await fetch(`/api/output?session=${encodeURIComponent(token)}`);
      if (!response.ok) {
        throw new Error('Failed to fetch output');
      }
      const data = await response.json();
      setCards(Array.isArray(data) ? data : data.cards || []);
      setOutputStatus('idle');
    } catch (err) {
      setOutputStatus('idle');
      pushToast(err.message, 'error');
    }
  }, [pushToast]);

  const fetchArtifacts = useCallback(async (token) => {
    if (!token) return;
    setLoadingArtifacts(true);
    try {
      const response = await fetch(`/api/artifacts?session=${encodeURIComponent(token)}`);
      if (!response.ok) {
        throw new Error('Failed to load artifacts');
      }
      const data = await response.json();
      setArtifactTree(data.tree || data);
    } catch (err) {
      pushToast(err.message, 'error');
    } finally {
      setLoadingArtifacts(false);
    }
  }, [pushToast]);

  useEffect(() => {
    if (sessionToken) {
      fetchPlan(sessionToken);
      fetchOutputs(sessionToken);
      fetchArtifacts(sessionToken);
    }
  }, [sessionToken, fetchPlan, fetchOutputs, fetchArtifacts]);

  const handleSavePlan = async (steps) => {
    if (!sessionToken) {
      pushToast('No active session', 'error');
      return;
    }
    try {
      const response = await fetch(`/api/plan/update?session=${encodeURIComponent(sessionToken)}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ steps: steps.map((step) => ({ id: step.id, text: step.text })) })
      });
      if (!response.ok) {
        throw new Error('Failed to save plan');
      }
      pushToast('Plan updated', 'success');
      setPlanSteps(steps);
    } catch (err) {
      pushToast(err.message, 'error');
    }
  };

  const handleNewCard = (card) => {
    setCards((prev) => [...prev, card]);
  };

  const pendingClarifier = useMemo(() => {
    return cards.find((card) => card.type === 'clarification' && !card.resolved);
  }, [cards]);

  const handleClarifierSubmit = async (promptText) => {
    if (!sessionToken) {
      pushToast('No active session', 'error');
      return;
    }
    try {
      const response = await fetch(`/api/jobs?session=${encodeURIComponent(sessionToken)}&text=${encodeURIComponent(promptText)}`, {
        method: 'POST'
      });
      if (!response.ok) {
        throw new Error('Failed to send clarifier response');
      }
      pushToast('Clarification sent', 'success');
    } catch (err) {
      pushToast(err.message, 'error');
    }
  };

  const handleRefreshOutputs = () => {
    if (sessionToken) {
      fetchOutputs(sessionToken);
    }
  };

  const handleRefreshArtifacts = () => {
    if (sessionToken) {
      fetchArtifacts(sessionToken);
    }
  };

  return (
    <div className="app-shell">
      <header className="app-header">
        <div className="brand">
          <span>Provider Control Center</span>
        </div>
        <nav className="nav">
          <button className={view === 'dashboard' ? 'active' : ''} onClick={() => setView('dashboard')}>
            Dashboard
          </button>
          <button className={view === 'docs' ? 'active' : ''} onClick={() => setView('docs')}>
            API Docs
          </button>
        </nav>
      </header>
      {view === 'dashboard' ? (
        <div className="page-content">
          <div className="column">
            <div className="panel">
              <SessionControls
                sessions={sessions}
                sessionToken={sessionToken}
                onNewSession={handleNewSession}
                onSelectSession={setSessionToken}
                onResetSession={handleResetSession}
              />
            </div>
            <div className="panel">
              <div className="section-title">
                <h2>Plan Preview</h2>
                <span>{loadingPlan ? 'Loading…' : ''}</span>
              </div>
              <PlanEditor steps={planSteps} onChange={setPlanSteps} onSave={handleSavePlan} />
            </div>
            <div className="panel">
              <div className="section-title">
                <h2>Output Cards</h2>
                <div className="tab-bar">
                  <button className="tab-button" onClick={handleRefreshOutputs}>Refresh</button>
                </div>
              </div>
              <OutputCards cards={cards} status={outputStatus} />
              <Streamer sessionToken={sessionToken} onCard={handleNewCard} onStatusChange={setOutputStatus} />
            </div>
          </div>
          <div className="column">
            <div className="panel">
              <div className="section-title">
                <h2>Artifacts</h2>
                <button className="button secondary" onClick={handleRefreshArtifacts}>Reload</button>
              </div>
              <ArtifactExplorer sessionToken={sessionToken} tree={artifactTree} loading={loadingArtifacts} pushToast={pushToast} />
            </div>
            <div className="panel">
              <h2>Clarifications</h2>
              <ClarifierPrompt card={pendingClarifier} onSubmit={handleClarifierSubmit} />
            </div>
          </div>
        </div>
      ) : (
        <div className="page-content">
          <div className="column" style={{ gridColumn: '1 / -1' }}>
            <div className="panel">
              <Docs />
            </div>
          </div>
        </div>
      )}
      <div className="toast-container" aria-live="polite">
        {toasts.map((toast) => (
          <div key={toast.id} className={`toast ${toast.type}`}>
            {toast.message}
          </div>
        ))}
      </div>
    </div>
  );
}

export default App;
