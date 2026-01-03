import React from 'react';

function SessionControls({ sessions, sessionToken, onNewSession, onSelectSession, onResetSession }) {
  return (
    <div>
      <h2>Session Control</h2>
      <div className="controls" style={{ marginTop: 8 }}>
        <button className="button" onClick={onNewSession}>New Session</button>
        <select value={sessionToken} onChange={(e) => onSelectSession(e.target.value)} className="input">
          <option value="" disabled>
            Select a session
          </option>
          {sessions.map((token) => (
            <option key={token} value={token}>
              {token}
            </option>
          ))}
        </select>
        <button className="button secondary" onClick={() => onResetSession(sessionToken)} disabled={!sessionToken}>
          Reset
        </button>
      </div>
    </div>
  );
}

export default SessionControls;
