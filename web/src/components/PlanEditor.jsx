import React, { useState } from 'react';
import { DragDropContext, Droppable, Draggable } from 'react-beautiful-dnd';

function PlanEditor({ steps, onChange, onSave }) {
  const [draftSteps, setDraftSteps] = useState(steps);

  React.useEffect(() => {
    setDraftSteps(steps);
  }, [steps]);

  const handleDragEnd = (result) => {
    if (!result.destination) return;
    const newSteps = Array.from(draftSteps);
    const [removed] = newSteps.splice(result.source.index, 1);
    newSteps.splice(result.destination.index, 0, removed);
    setDraftSteps(newSteps);
    onChange(newSteps);
  };

  const handleTextChange = (index, text) => {
    const updated = draftSteps.map((step, idx) => (idx === index ? { ...step, text } : step));
    setDraftSteps(updated);
    onChange(updated);
  };

  const addStep = () => {
    const newStep = { id: crypto.randomUUID(), text: 'New step' };
    const updated = [...draftSteps, newStep];
    setDraftSteps(updated);
    onChange(updated);
  };

  return (
    <div>
      <div className="controls" style={{ marginBottom: 12 }}>
        <button className="button" onClick={() => onSave(draftSteps)}>
          Save Plan
        </button>
        <button className="button secondary" onClick={addStep}>
          Add Step
        </button>
      </div>
      <DragDropContext onDragEnd={handleDragEnd}>
        <Droppable droppableId="plan">
          {(provided) => (
            <div className="plan-list" ref={provided.innerRef} {...provided.droppableProps}>
              {draftSteps.map((step, index) => (
                <Draggable key={step.id} draggableId={step.id} index={index}>
                  {(dragProvided) => (
                    <div
                      className="plan-item"
                      ref={dragProvided.innerRef}
                      {...dragProvided.draggableProps}
                      {...dragProvided.dragHandleProps}
                    >
                      <span style={{ fontWeight: 700 }}>#{index + 1}</span>
                      <input value={step.text} onChange={(e) => handleTextChange(index, e.target.value)} />
                    </div>
                  )}
                </Draggable>
              ))}
              {provided.placeholder}
            </div>
          )}
        </Droppable>
      </DragDropContext>
    </div>
  );
}

export default PlanEditor;
