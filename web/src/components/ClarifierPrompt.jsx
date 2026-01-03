import React, { useEffect, useState } from 'react';

function ClarifierPrompt({ card, onSubmit }) {
  const [value, setValue] = useState('');

  useEffect(() => {
    if (card) {
      setValue('');
    }
  }, [card]);

  if (!card) {
    return <div>No pending clarifications.</div>;
  }

  return (
    <div>
      <div style={{ marginBottom: 8 }}>{card.prompt || card.question || card.content}</div>
      <textarea
        className="input"
        rows={4}
        placeholder="Type your response"
        value={value}
        onChange={(e) => setValue(e.target.value)}
        style={{ width: '100%' }}
      />
      <div className="controls" style={{ marginTop: 8 }}>
        <button className="button" onClick={() => onSubmit(value)} disabled={!value.trim()}>
          Send
        </button>
      </div>
    </div>
  );
}

export default ClarifierPrompt;
