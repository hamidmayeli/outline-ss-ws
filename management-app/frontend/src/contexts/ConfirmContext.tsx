import React, { createContext, useCallback, useContext, useRef, useState } from 'react';

type ConfirmOptions = {
  title?: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
};

type ConfirmDialogState = {
  isOpen: boolean;
  options: ConfirmOptions;
};

type ConfirmContextValue = {
  confirm: (options: ConfirmOptions) => Promise<boolean>;
  dialog: ConfirmDialogState;
  handleConfirm: () => void;
  handleCancel: () => void;
};

const ConfirmContext = createContext<ConfirmContextValue | null>(null);

export const ConfirmProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [dialog, setDialog] = useState<ConfirmDialogState>({
    isOpen: false,
    options: {
      title: 'Confirm',
      message: '',
      confirmLabel: 'Confirm',
      cancelLabel: 'Cancel',
    },
  });
  const resolverRef = useRef<((value: boolean) => void) | null>(null);

  const closeDialog = useCallback(() => {
    setDialog((prev) => ({ ...prev, isOpen: false }));
    resolverRef.current = null;
  }, []);

  const confirm = useCallback(
    (options: ConfirmOptions) => {
      if (resolverRef.current) {
        resolverRef.current(false);
      }

      setDialog({
        isOpen: true,
        options: {
          title: options.title ?? 'Confirm',
          message: options.message,
          confirmLabel: options.confirmLabel ?? 'Confirm',
          cancelLabel: options.cancelLabel ?? 'Cancel',
        },
      });

      return new Promise<boolean>((resolve) => {
        resolverRef.current = resolve;
      });
    },
    []
  );

  const handleConfirm = useCallback(() => {
    if (resolverRef.current) {
      resolverRef.current(true);
    }
    closeDialog();
  }, [closeDialog]);

  const handleCancel = useCallback(() => {
    if (resolverRef.current) {
      resolverRef.current(false);
    }
    closeDialog();
  }, [closeDialog]);

  return (
    <ConfirmContext.Provider value={{ confirm, dialog, handleConfirm, handleCancel }}>
      {children}
    </ConfirmContext.Provider>
  );
};

export const useConfirm = () => {
  const context = useContext(ConfirmContext);
  if (!context) {
    throw new Error('useConfirm must be used within a ConfirmProvider');
  }
  return context.confirm;
};

export const useConfirmDialog = () => {
  const context = useContext(ConfirmContext);
  if (!context) {
    throw new Error('useConfirmDialog must be used within a ConfirmProvider');
  }
  return {
    dialog: context.dialog,
    handleConfirm: context.handleConfirm,
    handleCancel: context.handleCancel,
  };
};
