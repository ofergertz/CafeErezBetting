import React from 'react';
import { AlertTriangle } from 'lucide-react';

interface ErrorStateProps {
  message: string;
  onRetry?: () => void;
  retryLabel?: string;
  className?: string;
}

export const ErrorState: React.FC<ErrorStateProps> = ({ message, onRetry, retryLabel = 'נסה שוב', className = '' }) => {
  return (
    <div className={`flex flex-col items-center justify-center py-12 px-4 text-center ${className}`}>
      <div className="mb-4 text-red-400">
        <AlertTriangle size={48} strokeWidth={1.5} />
      </div>
      <p className="text-gray-700 font-medium text-base">{message}</p>
      {onRetry && (
        <button
          onClick={onRetry}
          className="mt-4 px-4 py-2 bg-[#2d6a4f] text-white rounded-lg text-sm font-medium hover:bg-[#245a41] transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#2d6a4f]"
        >
          {retryLabel}
        </button>
      )}
    </div>
  );
};

export default ErrorState;
