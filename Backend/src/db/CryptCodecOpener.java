package db;
import java.io.File;
import java.io.IOException;

import com.healthmarketscience.jackcess.Database;
import com.healthmarketscience.jackcess.DatabaseBuilder;
import com.healthmarketscience.jackcess.CryptCodecProvider;


import net.ucanaccess.jdbc.JackcessOpenerInterface;

public class CryptCodecOpener implements JackcessOpenerInterface {
      @Override
    public Database open(File fl, String password) throws IOException {
        DatabaseBuilder db = new DatabaseBuilder();
        db.setFile(fl);
        db.setCodecProvider(new CryptCodecProvider(password));
        db.setReadOnly(false);
        return db.open();
    }
}
