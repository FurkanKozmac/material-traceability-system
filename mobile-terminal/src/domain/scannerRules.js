export const evaluateScanAction = ({ result, mode, fifoOverride = false }) => {
  const allowedActions = result.allowedActions || [];
  const blockedOrConsumed = Boolean(result.blocked || result.status === 'CONSUMED');
  const expired = Boolean(result.expired);
  const fifoViolation = mode === 'PICK' && !result.fifoCompliant;

  if (blockedOrConsumed) return { canExecute: false, label: result.blocked ? 'BLOCKED' : 'CONSUMED', color: '#ef4444', blockedOrConsumed, expired, fifoViolation };
  if (expired) return { canExecute: false, label: 'EXPIRED', color: '#ef4444', blockedOrConsumed, expired, fifoViolation };
  if (fifoViolation && !fifoOverride) return { canExecute: false, label: 'FIFO WARNING', color: '#f59e0b', blockedOrConsumed, expired, fifoViolation };

  const owned = result.ownedByOrder;
  const orderStatus = result.order?.status;
  if (mode === 'PICK' && allowedActions.includes('PICK') && result.resolvedLine) return { canExecute: true, label: 'READY FOR PICK', color: '#10b981', blockedOrConsumed, expired, fifoViolation };
  if (mode === 'ISSUE' && orderStatus === 'ALLOCATED' && owned) return { canExecute: true, label: 'READY FOR ISSUE', color: '#10b981', blockedOrConsumed, expired, fifoViolation };
  if (mode === 'CONSUME' && orderStatus === 'ISSUED' && owned) return { canExecute: true, label: 'SPS CART VALIDATED', color: '#10b981', blockedOrConsumed, expired, fifoViolation };
  return { canExecute: false, label: 'INVALID STATUS', color: '#ef4444', blockedOrConsumed, expired, fifoViolation };
};
