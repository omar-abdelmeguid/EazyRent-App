import java.io.File;
import java.io.RandomAccessFile;
import java.nio.channels.FileChannel;
import java.nio.channels.FileLock;

public class SingleInstanceLock {
    private static FileLock lock;

    public static boolean lockInstance(String lockFile) {
        try {
            File file = new File(lockFile);
            FileChannel channel = new RandomAccessFile(file, "rw").getChannel();

            lock = channel.tryLock();
            if (lock == null) {
                channel.close();
                return false; // another instance is running
            }

            Runtime.getRuntime().addShutdownHook(new Thread(() -> {
                try {
                    lock.release();
                    channel.close();
                } catch (Exception e) { /* ignore */ }
            }));

            return true;
        } catch (Exception e) {
            return false;
        }
    }
}
